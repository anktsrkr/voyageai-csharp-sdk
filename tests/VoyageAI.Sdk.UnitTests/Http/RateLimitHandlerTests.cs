using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Http;

/// <summary>
/// White-box tests for <see cref="RateLimitHandler"/>. The handler is instantiated directly
/// with a stub <see cref="IOptionsMonitor{VoyageAIOptions}"/> set to
/// <see cref="VoyageAIOptions.ClientSideRpmLimit"/> = 1, which yields
/// <see cref="SlidingWindowRateLimiterOptions.PermitLimit"/> = 1 and
/// <see cref="SlidingWindowRateLimiterOptions.QueueLimit"/> = 2.
/// A <see cref="BlockingHandler"/> inner layer holds the single acquired request in flight;
/// two more queue behind it in the rate limiter; a fourth concurrent request overflows the
/// queue and fails the guard deterministically — no timing races, no sleeps.
/// </summary>
public class RateLimitHandlerTests
{
    /// <summary>
    /// <see cref="IOptionsMonitor{TOptions}"/> stub returning a fixed
    /// <see cref="VoyageAIOptions"/>. Used only to feed <c>ClientSideRpmLimit</c> at
    /// construction; the handler snapshots it in its ctor.
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

    [Fact]
    public async Task Acquire_Overflow_FailsDeterministicallyWithRateLimitException()
    {
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "k", ClientSideRpmLimit = 1 },
        };
        var blocker = new BlockingHandler();
        using var handler = new RateLimitHandler(monitor) { InnerHandler = blocker };
        using var invoker = new HttpMessageInvoker(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Step 1: Fire request #1 and wait for it to enter the BlockingHandler, which
        // proves it acquired the rate-limit permit and is holding it.
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/0");
        var task1 = invoker.SendAsync(request1, cts.Token);

        // Spin until the request has entered the inner handler (permit acquired).
        while (blocker.EnteredCount < 1)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(1, cts.Token);
        }

        // Step 2: Fire requests #2 and #3 — they queue behind the rate limiter
        // (PermitLimit=1, QueueLimit=2). They block on AcquireAsync, never reaching
        // the BlockingHandler.
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/1");
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/2");
        var task2 = invoker.SendAsync(request2, cts.Token);
        var task3 = invoker.SendAsync(request3, cts.Token);

        // Brief yield to let them enter the queue.
        await Task.Delay(50, cts.Token);

        // Step 3: Fire request #4 — the queue is full (1 acquired + 2 queued), so
        // AcquireAsync returns IsAcquired=false and the handler throws synchronously.
        using var request4 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/3");
        VoyageAIRateLimitException? capturedEx = null;
        try
        {
            using var response4 = await invoker.SendAsync(request4, cts.Token);
        }
        catch (VoyageAIRateLimitException ex)
        {
            capturedEx = ex;
        }

        capturedEx.Should().NotBeNull(
            because: "the 4th request overflows the rate-limit queue");
        capturedEx!.Message.Should().Contain("Client-side rate limit exceeded");

        // Unblock the held/queued requests so the test tears down cleanly.
        blocker.Release();
    }

    [Fact]
    public async Task AcquiredRequest_CompletesSuccessfullyWhenUnderLimit()
    {
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "k", ClientSideRpmLimit = 100 },
        };
        // A plain pass-through inner handler (no blocking).
        using var handler = new RateLimitHandler(monitor)
        {
            InnerHandler = new LambdaHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
        };
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/ok");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExceptionMessage_MentionsTheClientSideRpmLimitOption()
    {
        var monitor = new StubOptionsMonitor
        {
            CurrentValue = new VoyageAIOptions { ApiKey = "k", ClientSideRpmLimit = 1 },
        };
        var blocker = new BlockingHandler();
        using var handler = new RateLimitHandler(monitor) { InnerHandler = blocker };
        using var invoker = new HttpMessageInvoker(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Step 1: Acquire the single permit.
        var request1 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/0");
        var task1 = invoker.SendAsync(request1, cts.Token);
        while (blocker.EnteredCount < 1)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(1, cts.Token);
        }

        // Step 2: Fill the queue (2 spots).
        var request2 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/1");
        var request3 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/2");
        var task2 = invoker.SendAsync(request2, cts.Token);
        var task3 = invoker.SendAsync(request3, cts.Token);
        await Task.Delay(50, cts.Token);

        // Step 3: Overflow — assert message content.
        using var request4 = new HttpRequestMessage(HttpMethod.Get, "https://example.test/3");
        VoyageAIRateLimitException? capturedEx = null;
        try
        {
            using var response = await invoker.SendAsync(request4, cts.Token);
        }
        catch (VoyageAIRateLimitException ex)
        {
            capturedEx = ex;
        }

        capturedEx.Should().NotBeNull();
        capturedEx!.Message.Should().Contain(nameof(VoyageAIOptions.ClientSideRpmLimit));

        // Unblock the held request. Tasks #2/#3 remain queued; they will be
        // cancelled when cts is disposed below — cancelled tasks do not trigger
        // UnobservedTaskException, so no special handling is needed.
        blocker.Release();
    }

    /// <summary>A tiny inner handler built from a function, for the simplest happy path.</summary>
    private sealed class LambdaHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public LambdaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }
}
