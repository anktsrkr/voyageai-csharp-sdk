namespace VoyageAI;

/// <summary>
/// The transport-agnostic search stage of a Voyage RAG pipeline. Implementations are
/// transport-specific (MongoDB Atlas Vector Search, pgvector, Redis, Qdrant, ...) and
/// know how to embed the query and run a vector search against their store.
/// </summary>
/// <typeparam name="T">The record type the searcher produces.</typeparam>
/// <remarks>
/// The searcher is intentionally not given a <c>topK</c>: it returns its candidate pool
/// (e.g. 10–20 nearest neighbours), and the reranker narrows that down to the final
/// context window via <see cref="VoyageRerankerOptions.TopK"/>. A pipeline with no
/// reranker (<c>reranker: null</c>) uses the searcher's ordering as-is.
/// </remarks>
public interface IVoyageRagSearcher<T>
{
    /// <summary>
    /// Embeds the query and runs a vector search, returning the candidate pool in
    /// descending relevance order. The list's index is what the reranker maps back from.
    /// </summary>
    /// <param name="query">The raw user query string.</param>
    /// <param name="cancellationToken">Propagated through the search and embedding calls.</param>
    /// <returns>The candidate pool, ordered by descending search-time relevance.</returns>
    Task<IReadOnlyList<VoyageSearchResult<T>>> SearchAsync(
        string query, CancellationToken cancellationToken = default);
}
