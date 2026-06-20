using Microsoft.Agents.AI;

namespace VoyageAI;

/// <summary>
/// Options for <see cref="VoyageRagContextProvider"/>, scoped to the record type the
/// pipeline searches over. Being generic lets <see cref="ResultMapper"/> receive a
/// strongly-typed <see cref="VoyageSearchResult{T}"/> so it can read record fields directly
/// — no cast and no per-candidate wrapper allocation on the retrieval hot path.
/// </summary>
/// <typeparam name="T">The record type the searcher produces (e.g. a MongoDB document).</typeparam>
public sealed class VoyageRagContextProviderOptions<T>
{
    /// <summary>
    /// Rerank configuration. Non-nullable and defaults to <see cref="VoyageRerankerOptions"/>
    /// so consumers never need to coalesce a <see langword="null"/> with <c>new()</c>.
    /// </summary>
    public VoyageRerankerOptions RerankerOptions { get; set; } = new();

    /// <summary>
    /// Optional custom projection from a search candidate to a
    /// <see cref="TextSearchProvider.TextSearchResult"/>. The candidate's
    /// <see cref="VoyageSearchResult{T}.Record"/> is typed as <typeparamref name="T"/>, so the
    /// mapper reads record fields directly. When <see langword="null"/>, the provider uses a
    /// default mapper that populates <c>SourceName</c>/<c>SourceLink</c> when
    /// <typeparamref name="T"/> implements <see cref="IVoyageSearchResultMetadata"/>, and
    /// otherwise emits just <c>Text</c>.
    /// </summary>
    public Func<VoyageSearchResult<T>, TextSearchProvider.TextSearchResult>? ResultMapper { get; set; }

    /// <summary>
    /// Options forwarded verbatim to the <c>TextSearchProvider</c> constructor (search
    /// timing, memory limit, prompts). When <see langword="null"/>, the provider's
    /// defaults apply.
    /// </summary>
    public TextSearchProviderOptions? TextSearchOptions { get; set; }
}
