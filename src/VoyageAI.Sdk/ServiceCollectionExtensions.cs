using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using VoyageAI;
using VoyageAI.Clients;
using VoyageAI.Configuration;
using VoyageAI.Http;
using VoyageAI.Internal;

// ReSharper disable once CheckNamespace — public extension surface lives in Microsoft.Extensions.DependencyInjection.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration entry point for the Voyage AI SDK. Wires up
/// <see cref="VoyageAIOptions"/> (validated on start), three named
/// <see cref="System.Net.Http.HttpClient"/> instances (one per endpoint) with the standard
/// resilience pipeline + auth + client-side rate-limit handlers, and the three client
/// interfaces.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Voyage AI SDK, binding <see cref="VoyageAIOptions"/> from the
    /// <c>"VoyageAI"</c> configuration section with startup validation.
    /// </summary>
    public static IServiceCollection AddVoyageAI(this IServiceCollection services)
        => services.AddVoyageAI(configure: null);

    /// <summary>
    /// Registers the Voyage AI SDK with a code-first configuration callback that overrides
    /// the bound options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional callback to set <see cref="VoyageAIOptions"/> (e.g. API key, retry count).
    /// Applied after configuration binding.
    /// </param>
    public static IServiceCollection AddVoyageAI(
        this IServiceCollection services,
        Action<VoyageAIOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 1. Options — bind the "VoyageAI" section (when a configuration source is
        //    registered) and run the inline delegate. These are run on EVERY call: the
        //    options-configuration delegates intentionally accumulate, so a host can do
        //    AddVoyageAI(o => o.ApiKey = ...) and then AddVoyageEmbeddingGenerator() (which
        //    calls AddVoyageAI() internally with no callback) and both contributions land.
        var optionsBuilder = services.AddOptions<VoyageAIOptions>();

        if (services.HasConfigurationSource())
        {
            optionsBuilder.BindConfigurationWithTrimSuppression(VoyageAIOptions.SectionName);
        }

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        // 2. Infrastructure — registered exactly once via a sentinel descriptor. AddHttpClient
        //    is non-idempotent (it appends a typed-client descriptor + handler option
        //    delegates on every call), so guarding the typed clients, handlers, validator
        //    and ValidateOnStart behind this marker keeps AddVoyageAI safe to call
        //    repeatedly (which AddVoyageEmbeddingGenerator and AddVoyageReranker both do).
        if (services.All(d => d.ServiceType != typeof(VoyageAIInfrastructureMarker)))
        {
            services.Add(new ServiceDescriptor(
                typeof(VoyageAIInfrastructureMarker),
                _ => VoyageAIInfrastructureMarker.Instance,
                ServiceLifetime.Singleton));

            services.TryAddSingleton<IValidateOptions<VoyageAIOptions>, VoyageAIOptionsValidator>();
            optionsBuilder.ValidateOnStart();

            // Delegating handlers — transient: HttpClientFactory creates one per request.
            services.TryAddTransient<AuthenticationHandler>();
            services.TryAddTransient<RateLimitHandler>();

            // Named typed clients — one HttpClient per endpoint so each can be tuned
            // independently (timeouts, primary handlers). All share the same pipeline.
            services.AddHttpClient<IEmbeddingsClient, EmbeddingsClient>(
                    VoyageAIHttpClientNames.Embeddings, ConfigureClient)
                .AddVoyageResilienceHandler()
                .AddHttpMessageHandler<AuthenticationHandler>()
                .AddHttpMessageHandler<RateLimitHandler>();

            services.AddHttpClient<IMultimodalEmbeddingsClient, MultimodalEmbeddingsClient>(
                    VoyageAIHttpClientNames.MultimodalEmbeddings, ConfigureClient)
                .AddVoyageResilienceHandler()
                .AddHttpMessageHandler<AuthenticationHandler>()
                .AddHttpMessageHandler<RateLimitHandler>();

            services.AddHttpClient<IRerankClient, RerankClient>(
                    VoyageAIHttpClientNames.Rerank, ConfigureClient)
                .AddVoyageResilienceHandler()
                .AddHttpMessageHandler<AuthenticationHandler>()
                .AddHttpMessageHandler<RateLimitHandler>();
        }

        return services;
    }

    /// <summary>
    /// Sentinel registered once to make <c>AddVoyageAI</c> idempotent on its infrastructure
    /// (typed clients, delegating handlers, validator, <c>ValidateOnStart</c>) while still
    /// letting options-configuration delegates accumulate across repeated calls.
    /// </summary>
    private sealed class VoyageAIInfrastructureMarker
    {
        public static readonly VoyageAIInfrastructureMarker Instance = new();
    }

    /// <summary>
    /// Returns <see langword="true"/> when an <see cref="IConfiguration"/> service is
    /// registered, so <see cref="OptionsBuilderConfigurationExtensions.BindConfiguration"/>
    /// is only called when it can succeed.
    /// </summary>
    private static bool HasConfigurationSource(this IServiceCollection services)
        => services.Any(d => d.ServiceType == typeof(IConfiguration));

    /// <summary>
    /// Binds the configuration section. Suppressed for trim/AOT analysis because
    /// <see cref="VoyageAIOptions"/> is a plain POCO whose bindable properties are all
    /// public, statically discoverable instance properties — none are dynamically loaded.
    /// </summary>
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis", "IL2026",
        Justification = "VoyageAIOptions is a POCO with statically known public properties.")]
    [UnconditionalSuppressMessage(
        "AotAnalysis", "IL3050",
        Justification = "VoyageAIOptions is a POCO with statically known public properties.")]
    private static void BindConfigurationWithTrimSuppression(
        this OptionsBuilder<VoyageAIOptions> builder, string section) =>
        builder.BindConfiguration(section);

    /// <summary>
    /// Applies the shared <see cref="HttpClient"/> defaults: base address, timeout, and
    /// <c>Accept: application/json</c>.
    /// </summary>
    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<VoyageAIOptions>>().Value;

        client.BaseAddress = options.BaseAddress;
        client.Timeout = options.RequestTimeout;

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
