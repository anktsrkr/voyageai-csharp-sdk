using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using VoyageAI.Models;

namespace VoyageAI;

/// <summary>
/// Adapts the Voyage AI embeddings client to the
/// <see cref="IEmbeddingGenerator{TValue, TEmbedding}"/> abstraction. This is the keystone
/// type that lets Voyage plug into the standard .NET AI composition model: MEAI's
/// caching/batching/logging/OpenTelemetry middleware, MongoDB driver auto-embedding, and
/// <c>Microsoft.Extensions.VectorData</c> all consume this interface.
/// </summary>
/// <remarks>
/// The generator embeds text only (single strings and batches). Per-call values on
/// <see cref="EmbeddingGenerationOptions"/> (<see cref="EmbeddingGenerationOptions.ModelId"/>
/// and <see cref="EmbeddingGenerationOptions.Dimensions"/>) override the generator defaults.
/// </remarks>
public sealed class VoyageEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IEmbeddingsClient _client;
    private readonly VoyageEmbeddingGeneratorOptions _options;
    private readonly EmbeddingGeneratorMetadata _metadata;

    /// <summary>
    /// Initializes a new <see cref="VoyageEmbeddingGenerator"/> over the given client.
    /// </summary>
    /// <param name="client">The Voyage embeddings client used to satisfy generate calls.</param>
    /// <param name="options">
    /// Generator defaults. A <see langword="null"/> instance uses default options
    /// (<see cref="VoyageAIModels.Voyage3"/>, <see cref="InputType.Document"/>).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    public VoyageEmbeddingGenerator(IEmbeddingsClient client, VoyageEmbeddingGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? new VoyageEmbeddingGeneratorOptions();
        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "voyageai",
            providerUri: null,
            defaultModelId: _options.Model,
            defaultModelDimensions: _options.OutputDimension);
    }

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Materialize once: the API needs a count (for batch validation and result mapping),
        // and Enumerating lazily twice (validation + request) would break for one-shot streams.
        var inputs = values as IList<string> ?? values.ToList();
        if (inputs.Count == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>();
        }

        // Per-call overrides take precedence over generator defaults; fall back otherwise.
        var model = !string.IsNullOrWhiteSpace(options?.ModelId) ? options!.ModelId! : _options.Model;
        int? dimensions = options?.Dimensions ?? _options.OutputDimension;

        var request = new EmbeddingRequest
        {
            Model = model,
            Input = EmbeddingInput.From(inputs),
            InputType = _options.InputType,
            OutputDimension = dimensions,
            Truncation = _options.Truncation,
        };

        var response = await _client.EmbedAsync(request, cancellationToken).ConfigureAwait(false);

        // Map results in request order. The API returns one EmbeddingObject per input, indexed.
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(capacity: response.Data.Count);
        foreach (var item in response.Data.OrderBy(d => d.Index))
        {
            embeddings.Add(new Embedding<float>(item.AsMemory()));
        }

        embeddings.Usage = new UsageDetails
        {
            // Voyage reports a single combined total; map it to input tokens where the work
            // happened, leaving output tokens unset (embedding responses have no output tokens).
            InputTokenCount = response.Usage.TotalTokens,
            TotalTokenCount = response.Usage.TotalTokens,
        };

        embeddings.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        embeddings.AdditionalProperties[nameof(response.Model)] = response.Model;

        return embeddings;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Exposes <see cref="EmbeddingGeneratorMetadata"/> and the underlying
    /// <see cref="IEmbeddingsClient"/> so middleware and downstream components can introspect
    /// the generator or reach the typed client when they need non-embedding capabilities.
    /// </remarks>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        // serviceKey is unused: the generator is not keyed, but the interface contract carries it.
        if (serviceType == typeof(VoyageEmbeddingGenerator))
        {
            return this;
        }

        if (serviceType == typeof(IEmbeddingsClient))
        {
            return _client;
        }

        if (serviceType == typeof(EmbeddingGeneratorMetadata))
        {
            return _metadata;
        }

        return null;
    }

    /// <summary>
    /// Disposes the generator. The underlying <see cref="IEmbeddingsClient"/> is owned by the
    /// DI container (registered via HttpClientFactory), so it is not disposed here.
    /// </summary>
    public void Dispose()
    {
        // Intentionally a no-op: IEmbeddingsClient is managed/owned by the SDK's DI registration.
    }
}
