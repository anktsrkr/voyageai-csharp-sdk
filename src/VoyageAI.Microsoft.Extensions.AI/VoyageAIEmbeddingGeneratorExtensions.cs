using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VoyageAI;

// Public extension surface lives in Microsoft.Extensions.DependencyInjection, mirroring the
// main SDK's ServiceCollectionExtensions, so consumers add everything from one namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection entry points that register a Voyage AI-backed
/// <see cref="IEmbeddingGenerator{TValue, TEmbedding}"/> and return an
/// <see cref="EmbeddingGeneratorBuilder{TValue, TEmbedding}"/> so the standard MEAI
/// middleware (caching, batching, logging, OpenTelemetry) composes on top.
/// </summary>
public static class VoyageAIEmbeddingGeneratorExtensions
{
    /// <summary>
    /// Registers a <see cref="VoyageEmbeddingGenerator"/> as
    /// <see cref="IEmbeddingGenerator{TValue, TEmbedding}"/> and returns a builder that lets
    /// callers chain MEAI middleware (e.g. <c>.UseOpenTelemetry()</c>,
    /// <c>.UseDistributedCache()</c>).
    /// </summary>
    /// <remarks>
    /// Resolves <see cref="IEmbeddingsClient"/> from the container at activation time, so
    /// <c>AddVoyageAI</c> must be called first (or <see cref="IEmbeddingsClient"/> registered
    /// separately). This mirrors <c>AddVoyageReranker</c>: the transport is bootstrapped once
    /// by <c>AddVoyageAI</c>, and each adapter only adds its own binding on top. Callers can
    /// then mix any combination of <c>AddVoyageEmbeddingGenerator</c> / <c>AddVoyageReranker</c>
    /// in any order, in any quantity.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional callback to set <see cref="VoyageEmbeddingGeneratorOptions"/> (model,
    /// input type, output dimension, truncation). Applied to a default options instance.
    /// </param>
    /// <returns>
    /// An <see cref="EmbeddingGeneratorBuilder{TValue, TEmbedding}"/> for further composition.
    /// </returns>
    public static EmbeddingGeneratorBuilder<string, Embedding<float>> AddVoyageEmbeddingGenerator(
        this IServiceCollection services,
        Action<VoyageEmbeddingGeneratorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new VoyageEmbeddingGeneratorOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        // AddEmbeddingGenerator registers IEmbeddingGenerator<string, Embedding<float>> and
        // returns the builder. The factory resolves IEmbeddingsClient (registered by
        // AddVoyageAI) at activation; GetRequiredService throws a clear error if the caller
        // forgot to call AddVoyageAI first.
        return services.AddEmbeddingGenerator(
            sp => new VoyageEmbeddingGenerator(
                sp.GetRequiredService<IEmbeddingsClient>(),
                sp.GetService<VoyageEmbeddingGeneratorOptions>()),
            ServiceLifetime.Singleton);
    }
}
