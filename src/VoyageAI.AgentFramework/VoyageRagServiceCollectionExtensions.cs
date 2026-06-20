using Microsoft.Extensions.DependencyInjection.Extensions;
using VoyageAI;
using VoyageAI.Models;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection entry points that register the Voyage reranker
/// (<see cref="IVoyageReranker"/>) for use with <c>VoyageRagContextProvider</c> and the
/// <c>UseVoyageRag</c> wiring extensions. Lives in
/// <see cref="Microsoft.Extensions.DependencyInjection"/> so consumers add everything from
/// one namespace, mirroring the main SDK's <c>AddVoyageAI</c>.
/// </summary>
public static class VoyageRagServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="VoyageReranker"/> as a singleton
    /// <see cref="IVoyageReranker"/>, resolving <see cref="IRerankClient"/> from the
    /// container (so <c>AddVoyageAI</c> must be called first, or
    /// <see cref="IRerankClient"/> registered separately).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to set
    /// <see cref="VoyageRerankerOptions"/> (model, topK, truncation). Applied to a
    /// default options instance.</param>
    public static IServiceCollection AddVoyageReranker(
        this IServiceCollection services,
        Action<VoyageRerankerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new VoyageRerankerOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton<IVoyageReranker>(sp =>
        {
            var client = sp.GetRequiredService<IRerankClient>();
            var rerankerOptions = sp.GetService<VoyageRerankerOptions>() ?? options;
            return new VoyageReranker(client, rerankerOptions);
        });

        return services;
    }
}
