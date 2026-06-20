using VoyageAI.Models;

namespace VoyageAI;

/// <summary>
/// The rerank stage of a Voyage RAG pipeline. Wraps the Voyage rerank endpoint behind a
/// transport-agnostic surface so the provider can swap implementations in tests.
/// </summary>
public interface IVoyageReranker
{
    /// <summary>
    /// Reranks <paramref name="documents"/> against <paramref name="query"/>, returning
    /// results sorted by descending relevance. Each <see cref="RerankResult.Index"/> maps
    /// back to the position of the document within <paramref name="documents"/>.
    /// </summary>
    /// <param name="query">The query the documents are scored against.</param>
    /// <param name="documents">The candidate document texts, in submission order.</param>
    /// <param name="cancellationToken">Propagated to the HTTP call and retry pipeline.</param>
    Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query, IReadOnlyList<string> documents,
        CancellationToken cancellationToken = default);
}
