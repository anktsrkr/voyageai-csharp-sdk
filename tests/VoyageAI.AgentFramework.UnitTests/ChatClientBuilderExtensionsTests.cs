using Microsoft.Extensions.AI;

namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Tests for <see cref="ChatClientBuilderExtensions.UseVoyageRag{T}"/>: that it returns the
/// same builder (fluent) and the built client is not null.
/// </summary>
public class ChatClientBuilderExtensionsTests
{
    [Fact]
    public void UseVoyageRag_NullBuilder_Throws()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var act = () => ((ChatClientBuilder)null!).UseVoyageRag(searcher);
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseVoyageRag_ReturnsSameBuilder()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var builder = new ChatClientBuilder(new FakeChatClient());

        ChatClientBuilder returned = builder.UseVoyageRag(searcher);

        returned.Should().BeSameAs(builder);
    }

    [Fact]
    public void UseVoyageRag_BuiltClient_IsNotNull()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var builder = new ChatClientBuilder(new FakeChatClient());

        builder.UseVoyageRag(searcher);

        IChatClient built = builder.Build(null!);
        built.Should().NotBeNull();
    }

    [Fact]
    public void UseVoyageRag_WithReranker_AppliesConfigureCallback()
    {
        var searcher = new FakeVoyageRagSearcher<TestRecord>();
        var reranker = new VoyageReranker(new FakeRerankClient());
        var builder = new ChatClientBuilder(new FakeChatClient());

        builder.UseVoyageRag(searcher, reranker, o => o.RerankerOptions.TopK = 9);

        IChatClient built = builder.Build(null!);
        built.Should().NotBeNull();
    }
}
