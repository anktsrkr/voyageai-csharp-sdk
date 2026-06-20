namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Composition tests for <see cref="VoyageRagContextProvider.Create{T}"/> and the internal
/// <c>RunPipeline</c> stage. <c>TextSearchProvider.SearchAsync</c> is internal in the Agent
/// Framework, so tests drive the pipeline directly via the exposed <c>RunPipeline</c> seam
/// — which is the same code path the built provider delegates to.
/// </summary>
public class VoyageRagContextProviderTests
{
    private static IReadOnlyList<VoyageSearchResult<T>> Candidates<T>(
        params (T Record, string Text)[] items)
        => items.Select(i => new VoyageSearchResult<T> { Record = i.Record, Text = i.Text }).ToList();

    /// <summary>
    /// Runs the searcher, then exercises the production pipeline (rerank + mapper) via the
    /// internal seam — the same delegate the built <c>TextSearchProvider</c> invokes.
    /// </summary>
    private static Task<IReadOnlyList<TextSearchProvider.TextSearchResult>> SearchAsync<T>(
        IVoyageRagSearcher<T> searcher,
        IVoyageReranker? reranker = null,
        VoyageRagContextProviderOptions<T>? options = null,
        string query = "q")
    {
        options ??= new VoyageRagContextProviderOptions<T>();
        return SearchAsync(searcher.SearchAsync, reranker, options, query);
    }

    private static async Task<IReadOnlyList<TextSearchProvider.TextSearchResult>> SearchAsync<T>(
        Func<string, CancellationToken, Task<IReadOnlyList<VoyageSearchResult<T>>>> search,
        IVoyageReranker? reranker,
        VoyageRagContextProviderOptions<T> options,
        string query)
    {
        IReadOnlyList<VoyageSearchResult<T>> candidates = await search(query, CancellationToken.None);
        return await VoyageRagContextProvider.RunPipeline(
            query, candidates, reranker, options, CancellationToken.None);
    }

    [Fact]
    public void Create_NullSearcher_Throws()
    {
        var act = () => VoyageRagContextProvider.Create<TestRecord>(
            (IVoyageRagSearcher<TestRecord>)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("searcher");
    }

    [Fact]
    public async Task Create_SearchReturnsEmpty_ReturnsEmptyResults()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>
        {
            NextResults = Array.Empty<VoyageSearchResult<TestRecord>>()
        };
        var reranker = new FakeRerankClient();
        VoyageRagContextProviderOptions<TestRecord> options = new();

        var results = await SearchAsync(searcher, new VoyageReranker(reranker), options);

        results.Should().BeEmpty();
        reranker.CallCount.Should().Be(0); // no candidates → no rerank
    }

    [Fact]
    public async Task Create_NullReranker_PassesSearcherOrderingThrough()
    {
        var candidates = Candidates(
            (new TestRecord("0", "alpha", "Src A", "https://a"), "alpha"),
            (new TestRecord("1", "beta", "Src B", "https://b"), "beta"));
        var searcher = new FakeVoyageRagSearcher<TestRecord> { NextResults = candidates };

        var results = await SearchAsync<TestRecord>(searcher, reranker: null);

        results.Select(r => r.Text).Should().Equal(["alpha", "beta"]);
    }

    [Fact]
    public async Task Create_WithReranker_ReordersByIndexAndProjectsMetadata()
    {
        // Searcher returns A, B, C; fake reranker returns indices [2, 0] (C then A),
        // proving the provider maps the reranker's index back to the original candidate.
        var candidates = Candidates(
            (new TestRecord("0", "alpha", "Src A", "https://a"), "alpha"),
            (new TestRecord("1", "beta", "Src B", "https://b"), "beta"),
            (new TestRecord("2", "gamma", "Src C", "https://c"), "gamma"));
        var searcher = new FakeVoyageRagSearcher<TestRecord> { NextResults = candidates };
        var reranker = new FakeRerankClient
        {
            NextResults =
            [
                new() { Index = 2, RelevanceScore = 0.9f },
                new() { Index = 0, RelevanceScore = 0.5f }
            ]
        };

        var results = (await SearchAsync(searcher, new VoyageReranker(reranker), new())).ToList();

        results.Should().HaveCount(2);
        results[0].Text.Should().Be("gamma"); // index 2 → candidate C
        results[0].SourceName.Should().Be("Src C");
        results[0].SourceLink.Should().Be("https://c");
        results[0].RawRepresentation.Should().Be(candidates[2].Record);
        results[1].Text.Should().Be("alpha"); // index 0 → candidate A
        results[1].SourceName.Should().Be("Src A");

        // The reranker was handed the candidate texts in submission order.
        reranker.LastRequest!.Documents.Should().Equal(["alpha", "beta", "gamma"]);
        reranker.LastRequest.Query.Should().Be("q");
    }

    [Fact]
    public async Task Create_TopKLimitsReturnedCount()
    {
        var candidates = Candidates(
            (new PlainRecord("0", "a"), "a"),
            (new PlainRecord("1", "b"), "b"),
            (new PlainRecord("2", "c"), "c"));
        var searcher = new FakeVoyageRagSearcher<PlainRecord> { NextResults = candidates };
        // Reranker returns all three (identity), but TopK=2 should cap the output.
        var reranker = new FakeRerankClient();
        var options = new VoyageRagContextProviderOptions<PlainRecord> { RerankerOptions = { TopK = 2 } };

        var results = await SearchAsync(searcher, new VoyageReranker(reranker), options);

        results.Should().HaveCount(2);
        results.Select(r => r.Text).Should().Equal(["a", "b"]);
    }

    [Fact]
    public async Task Create_DefaultMapper_PlainRecordEmitsOnlyText()
    {
        var candidates = Candidates((new PlainRecord("0", "body"), "the text"));
        var searcher = new FakeVoyageRagSearcher<PlainRecord> { NextResults = candidates };

        var result = (await SearchAsync<PlainRecord>(searcher, reranker: null)).Single();

        result.Text.Should().Be("the text");
        result.SourceName.Should().BeNull();
        result.SourceLink.Should().BeNull();
        result.RawRepresentation.Should().Be(candidates[0].Record);
    }

    [Fact]
    public async Task Create_CustomResultMapper_IsUsed()
    {
        var candidates = Candidates((new PlainRecord("42", "body"), "text"));
        var searcher = new FakeVoyageRagSearcher<PlainRecord> { NextResults = candidates };
        var options = new VoyageRagContextProviderOptions<PlainRecord>
        {
            // The mapper receives a strongly-typed candidate — no cast, no boxing.
            ResultMapper = c => new()
            {
                Text = c.Text,
                SourceName = "id=" + c.Record.Id
            }
        };

        var result = (await SearchAsync(searcher, reranker: null, options)).Single();

        result.Text.Should().Be("text");
        result.SourceName.Should().Be("id=42");
    }

    [Fact]
    public async Task Create_FuncOverload_AppliesConfigureCallback()
    {
        var candidates = Candidates(
            (new TestRecord("0", "a", "A", "https://a"), "a"),
            (new TestRecord("1", "b", "B", "https://b"), "b"));
        var captured = new List<string>();
        Func<string, CancellationToken, Task<IReadOnlyList<VoyageSearchResult<TestRecord>>>> search =
            (query, ct) =>
            {
                captured.Add(query);
                return Task.FromResult<IReadOnlyList<VoyageSearchResult<TestRecord>>>(candidates);
            };
        var reranker = new FakeRerankClient();
        var options = new VoyageRagContextProviderOptions<TestRecord> { RerankerOptions = { TopK = 1 } };

        var results = await SearchAsync(search, new VoyageReranker(reranker), options, query: "hello");

        captured.Should().Equal(["hello"]);
        results.Should().HaveCount(1); // TopK=1
        results.Single().Text.Should().Be("a"); // identity ordering keeps the first candidate
    }

    [Fact]
    public async Task Create_OutOfRangeRerankIndex_IsSkippedNotThrown()
    {
        // A buggy/fake reranker returns an index beyond the candidate list. The provider
        // must not throw inside the agent's retrieval path; it skips the bad entry.
        var candidates = Candidates((new PlainRecord("0", "a"), "a"));
        var searcher = new FakeVoyageRagSearcher<PlainRecord> { NextResults = candidates };
        var reranker = new FakeRerankClient
        {
            NextResults = [new() { Index = 5, RelevanceScore = 0.9f }]
        };

        var act = async () => await SearchAsync(searcher, new VoyageReranker(reranker));

        await act.Should().NotThrowAsync();
    }
}
