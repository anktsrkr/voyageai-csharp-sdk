using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace VoyageAI;

/// <summary>
/// Composes a search stage (<see cref="IVoyageRagSearcher{T}"/>) with an optional rerank
/// stage (<see cref="IVoyageReranker"/>) and hands the result to the Agent Framework's
/// <see cref="TextSearchProvider"/>. <see cref="TextSearchProvider"/> is sealed and takes a
/// search delegate in its constructor, so this factory builds that delegate by wiring the
/// two stages together — the only piece that knows about both search and rerank.
/// </summary>
public static class VoyageRagContextProvider
{
    /// <summary>
    /// Creates a <see cref="TextSearchProvider"/> from a searcher instance and an optional
    /// reranker. Pass <see langword="null"/> for <paramref name="reranker"/> to use the
    /// searcher's ordering as-is (reranking skipped).
    /// </summary>
    /// <param name="searcher">The transport-specific search stage.</param>
    /// <param name="reranker">The optional Voyage rerank stage. <see langword="null"/>
    /// skips reranking.</param>
    /// <param name="options">Provider options (rerank config, result mapper, text-search
    /// options). <see langword="null"/> uses defaults.</param>
    /// <param name="loggerFactory">Optional logger factory forwarded to the provider.</param>
    public static TextSearchProvider Create<T>(
        IVoyageRagSearcher<T> searcher,
        IVoyageReranker? reranker = null,
        VoyageRagContextProviderOptions<T>? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(searcher);
        options ??= new VoyageRagContextProviderOptions<T>();

        Func<string, CancellationToken, Task<IReadOnlyList<VoyageSearchResult<T>>>> searchAsync =
            (query, ct) => searcher.SearchAsync(query, ct);

        return Build(searchAsync, reranker, options, loggerFactory);
    }

    /// <summary>
    /// Creates a <see cref="TextSearchProvider"/> from an ad-hoc search delegate, so a
    /// caller can wire RAG without defining a <see cref="IVoyageRagSearcher{T}"/>
    /// implementation class.
    /// </summary>
    /// <param name="searchAsync">The search stage: query → candidate pool.</param>
    /// <param name="reranker">The optional Voyage rerank stage. <see langword="null"/>
    /// skips reranking.</param>
    /// <param name="configure">Optional callback to set provider options.</param>
    /// <param name="loggerFactory">Optional logger factory forwarded to the provider.</param>
    public static TextSearchProvider Create<T>(
        Func<string, CancellationToken, Task<IReadOnlyList<VoyageSearchResult<T>>>> searchAsync,
        IVoyageReranker? reranker = null,
        Action<VoyageRagContextProviderOptions<T>>? configure = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(searchAsync);
        var options = new VoyageRagContextProviderOptions<T>();
        configure?.Invoke(options);

        return Build(searchAsync, reranker, options, loggerFactory);
    }

    private static TextSearchProvider Build<T>(
        Func<string, CancellationToken, Task<IReadOnlyList<VoyageSearchResult<T>>>> searchAsync,
        IVoyageReranker? reranker,
        VoyageRagContextProviderOptions<T> options,
        ILoggerFactory? loggerFactory)
    {
        // The delegate handed to TextSearchProvider: search → optional rerank → project.
        // The body forwards to RunPipeline so the pipeline is independently testable
        // without going through the internal TextSearchProvider.SearchAsync seam.
        async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
            string query, CancellationToken ct)
        {
            IReadOnlyList<VoyageSearchResult<T>> candidates = await searchAsync(query, ct)
                .ConfigureAwait(false);
            return await RunPipeline(query, candidates, reranker, options, ct)
                .ConfigureAwait(false);
        }

        return new TextSearchProvider(SearchAsync, options.TextSearchOptions, loggerFactory);
    }

    /// <summary>
    /// Runs the rerank → project stages over an already-searched candidate pool. Exposed as
    /// <see langword="internal"/> for unit tests: <c>TextSearchProvider</c>'s own search
    /// method is <see langword="internal"/> in the Agent Framework, so tests cannot drive the
    /// built provider end-to-end. This is the direct path.
    /// </summary>
    /// <param name="query">The original query (forwarded to the reranker).</param>
    /// <param name="candidates">The searcher's candidate pool, ordered by descending
    /// search-time relevance. The list's index is what the reranker maps back from.</param>
    /// <param name="reranker">The optional rerank stage. <see langword="null"/> uses the
    /// searcher's ordering as-is.</param>
    /// <param name="options">Provider options (rerank config + result mapper). The mapper is
    /// resolved here — default or custom — so this is the single resolution site.</param>
    /// <param name="cancellationToken">Propagated through the rerank call.</param>
    internal static async Task<IReadOnlyList<TextSearchProvider.TextSearchResult>> RunPipeline<T>(
        string query,
        IReadOnlyList<VoyageSearchResult<T>> candidates,
        IVoyageReranker? reranker,
        VoyageRagContextProviderOptions<T> options,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        IReadOnlyList<VoyageSearchResult<T>> ordered = reranker is null
            ? candidates
            : await RerankAsync(query, candidates, reranker, options.RerankerOptions, cancellationToken)
                .ConfigureAwait(false);

        // Project each candidate to a TextSearchResult via the (default or custom) mapper.
        // The mapper is typed against T, so it reads record fields directly — no cast, no
        // per-candidate wrapper allocation.
        Func<VoyageSearchResult<T>, TextSearchProvider.TextSearchResult> mapper =
            options.ResultMapper ?? DefaultResultMapper;
        var results = new List<TextSearchProvider.TextSearchResult>(ordered.Count);
        foreach (VoyageSearchResult<T> candidate in ordered)
        {
            results.Add(mapper(candidate));
        }

        return results;
    }

    private static async Task<IReadOnlyList<VoyageSearchResult<T>>> RerankAsync<T>(
        string query,
        IReadOnlyList<VoyageSearchResult<T>> candidates,
        IVoyageReranker reranker,
        VoyageRerankerOptions rerankerOptions,
        CancellationToken ct)
    {
        // Score the candidate texts and map the reranker's index back to the original record.
        string[] texts = new string[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            texts[i] = candidates[i].Text;
        }

        IReadOnlyList<Models.RerankResult> reranked = await reranker
            .RerankAsync(query, texts, ct)
            .ConfigureAwait(false);

        // TopK is already enforced by the rerank request (RerankerOptions.TopK); but if the
        // reranker returns more than requested, honour it as an upper bound here too.
        int limit = rerankerOptions.TopK ?? int.MaxValue;
        var result = new List<VoyageSearchResult<T>>(Math.Min(reranked.Count, limit));
        foreach (Models.RerankResult item in reranked)
        {
            if (result.Count >= limit)
            {
                break;
            }

            // Guard against an out-of-range index from a buggy/fake reranker rather than
            // throwing inside the agent's retrieval path.
            if ((uint)item.Index < (uint)candidates.Count)
            {
                result.Add(candidates[item.Index]);
            }
        }

        return result;
    }

    /// <summary>
    /// Default projection: populates <c>SourceName</c>/<c>SourceLink</c> when the record
    /// implements <see cref="IVoyageSearchResultMetadata"/>, attaches the record as
    /// <c>RawRepresentation</c>, and always sets <c>Text</c>.
    /// </summary>
    private static TextSearchProvider.TextSearchResult DefaultResultMapper<T>(
        VoyageSearchResult<T> candidate)
    {
        var result = new TextSearchProvider.TextSearchResult
        {
            Text = candidate.Text,
            RawRepresentation = candidate.Record
        };

        if (candidate.Record is IVoyageSearchResultMetadata metadata)
        {
            result.SourceName = metadata.SourceName;
            result.SourceLink = metadata.SourceLink;
        }

        return result;
    }
}
