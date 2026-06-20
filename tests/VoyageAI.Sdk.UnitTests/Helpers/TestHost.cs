using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace VoyageAI.Tests.Unit.Helpers;

/// <summary>
/// Builds a real DI container (<see cref="AddVoyageAI"/> + <see cref="IHttpClientFactory"/>)
/// with a <see cref="MockHttpMessageHandler"/> as the innermost (primary) handler. This
/// exercises the full live pipeline — authentication, client-side rate limiting, and the
/// Polly resilience stack — against deterministic canned responses, exactly as a consumer
/// would experience them.
/// </summary>
internal sealed class TestHost : IDisposable
{
    /// <summary>The mock handler backing every named HttpClient. Configure responses here.</summary>
    public MockHttpMessageHandler MockHttp { get; } = new();

    private ServiceProvider? _provider;
    private bool _disposed;

    /// <summary>
    /// Builds the service provider. <paramref name="configure"/> overrides options (API key,
    /// retry counts, breaker ratio, etc.). Uses a fixed test key so the auth handler always
    /// has something to inject unless a test sets its own.
    /// </summary>
    public ServiceProvider Build(
        Action<VoyageAIOptions>? configure = null,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        var services = new ServiceCollection();

        // AddLogging so ILogger<T> resolves; tests that need to inspect logs inject their
        // own RecordingLoggerProvider before calling Build.
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));

        services.AddVoyageAI(options =>
        {
            options.ApiKey = "test-api-key";
            // Tighten the client-side RPM guard so rate-limit tests can overflow deterministically
            // without changing the default for every other test.
            configure?.Invoke(options);
        });

        // Re-register the three named clients' primary handler as the mock. The SDK registers
        // the clients via AddVoyageAI; we attach the mock by configuring each named client's
        // primary handler. HttpClientFactory lets the outer delegating handlers (auth,
        // rate-limit, Polly) still run.
        ConfigurePrimaryHandler(services, VoyageAIHttpClientNames.Embeddings, configureClient);
        ConfigurePrimaryHandler(services, VoyageAIHttpClientNames.MultimodalEmbeddings, configureClient);
        ConfigurePrimaryHandler(services, VoyageAIHttpClientNames.Rerank, configureClient);

        _provider = services.BuildServiceProvider();
        return _provider;
    }

    private void ConfigurePrimaryHandler(
        ServiceCollection services, string name, Action<IHttpClientBuilder>? configureClient)
    {
        var builder = services.AddHttpClient(name)
            .ConfigurePrimaryHttpMessageHandler(() => MockHttp);
        configureClient?.Invoke(builder);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        MockHttp.Dispose();
        _provider?.Dispose();
        _disposed = true;
    }
}
