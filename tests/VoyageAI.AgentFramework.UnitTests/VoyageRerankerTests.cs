namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Tests for <see cref="VoyageReranker"/>: that it maps options onto a
/// <see cref="RerankRequest"/> (model, topK, truncation, returnDocuments=false), short-circuits
/// empty document lists, rejects null arguments, and delegates to the underlying client.
/// </summary>
public class VoyageRerankerTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var act = () => new VoyageReranker(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Fact]
    public void Constructor_NullOptions_FallsBackToDefaults()
    {
        var reranker = new VoyageReranker(new FakeRerankClient(), options: null);

        reranker.Should().NotBeNull();
    }

    [Fact]
    public async Task RerankAsync_MapsOptionsOntoRequest()
    {
        var fake = new FakeRerankClient();
        var reranker = new VoyageReranker(fake, new VoyageRerankerOptions
        {
            Model = VoyageAIModels.Rerank2Lite,
            TopK = 3,
            Truncation = false
        });

        await reranker.RerankAsync("query", ["a", "b"]);

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.Query.Should().Be("query");
        fake.LastRequest.Documents.Should().Equal(["a", "b"]);
        fake.LastRequest.Model.Should().Be(VoyageAIModels.Rerank2Lite);
        fake.LastRequest.TopK.Should().Be(3);
        fake.LastRequest.Truncation.Should().BeFalse();
        // Index-based mapping requires the documents not be echoed back over the wire.
        fake.LastRequest.ReturnDocuments.Should().BeFalse();
    }

    [Fact]
    public async Task RerankAsync_NullDocuments_Throws()
    {
        var reranker = new VoyageReranker(new FakeRerankClient());
        var act = async () => await reranker.RerankAsync("query", null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("documents");
    }

    [Fact]
    public async Task RerankAsync_EmptyDocuments_ShortCircuitsWithoutCallingClient()
    {
        var fake = new FakeRerankClient();
        var reranker = new VoyageReranker(fake);

        IReadOnlyList<RerankResult> results = await reranker.RerankAsync("query", []);

        results.Should().BeEmpty();
        fake.CallCount.Should().Be(0); // empty list never reaches the endpoint
    }

    [Fact]
    public async Task RerankAsync_DelegatesToClientAndReturnsData()
    {
        var canned = new[]
        {
            new RerankResult { Index = 1, RelevanceScore = 0.9f },
            new RerankResult { Index = 0, RelevanceScore = 0.4f }
        };
        var fake = new FakeRerankClient { NextResults = canned };
        var reranker = new VoyageReranker(fake);

        IReadOnlyList<RerankResult> results = await reranker.RerankAsync("q", ["a", "b"]);

        fake.CallCount.Should().Be(1);
        results.Should().Equal(canned);
    }

    [Fact]
    public void DefaultOptions_AreSensible()
    {
        var options = new VoyageRerankerOptions();

        options.Model.Should().Be(VoyageAIModels.Rerank2);
        options.TopK.Should().Be(5);
        options.Truncation.Should().BeTrue();
    }
}
