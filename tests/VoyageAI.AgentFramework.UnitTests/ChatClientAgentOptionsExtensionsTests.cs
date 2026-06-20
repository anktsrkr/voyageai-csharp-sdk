namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Tests for <see cref="ChatClientAgentOptionsExtensions.UseVoyageRag{T}"/>: that it appends
/// a Voyage-composed provider to <see cref="ChatClientAgentOptions.AIContextProviders"/>,
/// applies the configure callback, and preserves pre-existing providers.
/// </summary>
public class ChatClientAgentOptionsExtensionsTests
{
    [Fact]
    public void UseVoyageRag_NullOptions_Throws()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var act = () => ((ChatClientAgentOptions)null!).UseVoyageRag(searcher);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void UseVoyageRag_AppendsVoyageProvider_WhenListWasNull()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var options = new ChatClientAgentOptions();

        // AIContextProviders defaults to null; the extension must initialize it.
        options.AIContextProviders.Should().BeNull();

        ChatClientAgentOptions returned = options.UseVoyageRag(searcher);

        returned.Should().BeSameAs(options);
        options.AIContextProviders.Should().NotBeNull();
        options.AIContextProviders.Should().ContainSingle()
            .Which.Should().BeOfType<TextSearchProvider>();
    }

    [Fact]
    public void UseVoyageRag_PreservesExistingProviders()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        TextSearchProvider existing = VoyageRagContextProvider.Create(searcher);
        var options = new ChatClientAgentOptions
        {
            AIContextProviders = [existing]
        };

        options.UseVoyageRag(searcher);

        options.AIContextProviders.Should().HaveCount(2);
        options.AIContextProviders.First().Should().BeSameAs(existing);
    }

    [Fact]
    public async Task UseVoyageRag_AppliesConfigureCallback()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var reranker = new VoyageReranker(new FakeRerankClient());
        var options = new ChatClientAgentOptions();

        // Capture the configured TopK so we can verify it flows through to the pipeline.
        // TextSearchProvider.SearchAsync is internal, so the callback is verified by
        // re-running the same pipeline (RunPipeline) the built provider delegates to.
        VoyageRagContextProviderOptions<TestRecord> providerOptions = new();
        options.UseVoyageRag(searcher, reranker, o =>
        {
            providerOptions = o;
            o.RerankerOptions.TopK = 9;
        });

        var provider = (TextSearchProvider)options.AIContextProviders!.Single();
        provider.Should().NotBeNull();

        var candidates = Enumerable.Range(0, 12)
            .Select(i => new VoyageSearchResult<TestRecord>
            {
                Record = new TestRecord(i.ToString(), i.ToString(), "s", "u"),
                Text = i.ToString()
            })
            .ToList();

        var results = await VoyageRagContextProvider.RunPipeline(
            "q", candidates, reranker, providerOptions, CancellationToken.None);

        results.Should().HaveCount(9); // TopK=9 caps a 12-candidate pool
    }
}
