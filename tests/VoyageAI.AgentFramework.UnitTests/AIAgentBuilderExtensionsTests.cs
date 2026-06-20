using Microsoft.Extensions.AI;

namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Tests for <see cref="AIAgentBuilderExtensions.UseVoyageRag{T}"/>: that it returns the
/// same builder (fluent) and the wrapped agent can be built without throwing.
/// </summary>
public class AIAgentBuilderExtensionsTests
{
    [Fact]
    public void UseVoyageRag_NullBuilder_Throws()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var act = () => ((AIAgentBuilder)null!).UseVoyageRag(searcher);
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseVoyageRag_ReturnsSameBuilder()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var agent = new ChatClientAgent(new FakeChatClient());
        var builder = new AIAgentBuilder(agent);

        AIAgentBuilder returned = builder.UseVoyageRag(searcher);

        returned.Should().BeSameAs(builder);
    }

    [Fact]
    public void UseVoyageRag_BuiltAgent_IsNotNull()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var agent = new ChatClientAgent(new FakeChatClient());
        var builder = new AIAgentBuilder(agent);

        builder.UseVoyageRag(searcher);

        AIAgent built = builder.Build(null!);

        built.Should().NotBeNull();
    }

    [Fact]
    public void UseVoyageRag_WithReranker_AppliesConfigureCallback()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var reranker = new VoyageReranker(new FakeRerankClient());
        var agent = new ChatClientAgent(new FakeChatClient());
        var builder = new AIAgentBuilder(agent);

        builder.UseVoyageRag(searcher, reranker, o => o.RerankerOptions.TopK = 9);

        // The extension should not throw, and Build should produce a valid agent.
        AIAgent built = builder.Build(null!);
        built.Should().NotBeNull();
    }
}
