using VoyageAI.Models;

namespace VoyageAI;

/// <summary>
/// Default <see cref="IVoyageReranker"/> over the Voyage SDK's <see cref="IRerankClient"/>.
/// Builds a <see cref="RerankRequest"/> with <see cref="RerankRequest.ReturnDocuments"/>
/// set to <see langword="false"/>: the provider maps <see cref="RerankResult.Index"/>
/// back to the original candidate records, so echoing document text back over the wire
/// would be redundant.
/// </summary>
public sealed class VoyageReranker : IVoyageReranker
{
    private readonly IRerankClient _client;
    private readonly VoyageRerankerOptions _options;

    /// <summary>Creates a reranker over <paramref name="client"/> with default options.</summary>
    /// <param name="client">The Voyage rerank client.</param>
    public VoyageReranker(IRerankClient client)
        : this(client, new VoyageRerankerOptions())
    {
    }

    /// <summary>Creates a reranker over <paramref name="client"/> with the given options.</summary>
    /// <param name="client">The Voyage rerank client.</param>
    /// <param name="options">Rerank configuration. A <see langword="null"/> reference is
    /// treated as the default options so consumers never need to coalesce.</param>
    public VoyageReranker(IRerankClient client, VoyageRerankerOptions? options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new VoyageRerankerOptions();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query, IReadOnlyList<string> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            // No candidates to score; the rerank endpoint would reject an empty list, so
            // short-circuit and preserve the caller's invariant (empty in → empty out).
            return Array.Empty<RerankResult>();
        }

        var request = new RerankRequest
        {
            Model = _options.Model,
            Query = query,
            Documents = documents,
            TopK = _options.TopK,
            Truncation = _options.Truncation,
            ReturnDocuments = false // index-based mapping preserves transport metadata.
        };

        RerankResponse response = await _client.RerankAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return response.Data;
    }
}
