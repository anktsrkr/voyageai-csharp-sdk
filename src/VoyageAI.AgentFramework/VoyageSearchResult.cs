namespace VoyageAI;

/// <summary>
/// A single retrieval candidate returned by an <see cref="IVoyageRagSearcher{T}"/>.
/// </summary>
/// <typeparam name="T">The record type the searcher produces (e.g. a MongoDB document).</typeparam>
/// <remarks>
/// <para>
/// Position matters: the candidate's index within the searcher's result list is what the
/// Voyage reranker echoes back via <see cref="Models.RerankResult.Index"/>. The provider
/// maps that index back to the original <see cref="Record"/> so transport-specific
/// metadata survives the rerank stage without re-fetching.
/// </para>
/// <para>
/// <see cref="Text"/> is the string the reranker scores and the value injected into the
/// agent's context. <see cref="Record"/> carries anything else the caller wants projected
/// into <c>TextSearchResult</c> (source links, ids, raw payloads).
/// </para>
/// </remarks>
public sealed class VoyageSearchResult<T>
{
    /// <summary>The transport-specific record (e.g. a MongoDB document).</summary>
    public required T Record { get; init; }

    /// <summary>The text the reranker scores and the agent receives as context.</summary>
    public required string Text { get; init; }
}
