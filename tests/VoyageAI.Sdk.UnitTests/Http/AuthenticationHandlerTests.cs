using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Http;

/// <summary>
/// White-box tests for <see cref="AuthenticationHandler"/>. The handler is instantiated
/// directly with a stub <see cref="IOptionsMonitor{VoyageAIOptions}"/> so we can assert
/// the <c>Authorization: Bearer</c> header injection and — critically — that the key is
/// resolved live on every request (not captured at construction), which is what makes
/// runtime key rotation work.
/// </summary>
[Collection("Environment")]
public class AuthenticationHandlerTests
{
    /// <summary>
    /// Minimal <see cref="IOptionsMonitor{TOptions}"/> that returns whatever the test has
    /// placed in <see cref="CurrentValue"/>. The .NET <c>OptionsWrapper</c> only
    /// implements <see cref="IOptions{TOptions}"/>, so this stub exists for the monitor.
    /// </summary>
    private sealed class StubOptionsMonitor : IOptionsMonitor<VoyageAIOptions>
    {
        public VoyageAIOptions CurrentValue { get; set; } = new();
        public VoyageAIOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<VoyageAIOptions, string?> listener)
            => new NullDisposable();

        private sealed class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Innermost handler that captures the request it receives and returns an empty 200,
    /// so the auth handler's only observable effect — the header — can be inspected.
    /// </summary>
    private sealed class CapturingHandler : DelegatingHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task SendAsync_InjectsBearerHeaderWithConfiguredKey()
    {
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "secret-key-42" },
        };
        var inner = new CapturingHandler();
        using var handler = new AuthenticationHandler(monitor);
        handler.InnerHandler = inner;
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/ping");
        await invoker.SendAsync(request, CancellationToken.None);

        inner.Captured.Should().NotBeNull();
        var auth = inner.Captured!.Headers.Authorization;
        auth.Should().NotBeNull();
        auth!.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("secret-key-42");
    }

    [Fact]
    public async Task SendAsync_ResolvesKeyLiveFromCurrentValue_AllowingRotation()
    {
        // The key is read on each SendAsync from CurrentValue, NOT cached in the ctor.
        // Returning a different key for the second call proves the handler supports
        // runtime key rotation (e.g. after options reload).
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "first-key" },
        };
        var inner = new CapturingHandler();
        using var handler = new AuthenticationHandler(monitor) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        // First request → first key.
        using var r1 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/a");
        await invoker.SendAsync(r1, CancellationToken.None);
        inner.Captured!.Headers.Authorization!.Parameter.Should().Be("first-key");

        // Rotate the live options; the same handler instance picks up the new key.
        monitor.CurrentValue = new VoyageAIOptions { ApiKey = "rotated-key" };

        using var r2 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/b");
        await invoker.SendAsync(r2, CancellationToken.None);
        inner.Captured!.Headers.Authorization!.Parameter.Should().Be("rotated-key");
    }

    [Fact]
    public async Task SendAsync_ResolvesApiKeyViaEnvFallbackWhenBlank()
    {
        // When ApiKey is blank the handler relies on ResolveApiKey()'s env fallback.
        var original = Environment.GetEnvironmentVariable(VoyageAIOptions.ApiKeyEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                VoyageAIOptions.ApiKeyEnvironmentVariable, "env-injected-key");

            var monitor = new StubOptionsMonitor
            {
                CurrentValue = new VoyageAIOptions { ApiKey = "" },
            };
            var inner = new CapturingHandler();
            using var handler = new AuthenticationHandler(monitor) { InnerHandler = inner };
            using var invoker = new HttpMessageInvoker(handler);

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/c");
            await invoker.SendAsync(request, CancellationToken.None);

            inner.Captured!.Headers.Authorization!.Parameter.Should().Be("env-injected-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                VoyageAIOptions.ApiKeyEnvironmentVariable, original);
        }
    }

    [Fact]
    public async Task SendAsync_OverwritesAnyPreExistingAuthorizationHeader()
    {
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "real-key" },
        };
        var inner = new CapturingHandler();
        using var handler = new AuthenticationHandler(monitor) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/d");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stale-caller-key");
        await invoker.SendAsync(request, CancellationToken.None);

        // The handler must replace a caller-supplied header, not leave a stale value.
        inner.Captured!.Headers.Authorization!.Parameter.Should().Be("real-key");
    }
}
