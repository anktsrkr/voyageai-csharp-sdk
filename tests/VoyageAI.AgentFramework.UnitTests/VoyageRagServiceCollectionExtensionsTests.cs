namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Tests for <see cref="Microsoft.Extensions.DependencyInjection.VoyageRagServiceCollectionExtensions.AddVoyageReranker"/>.
/// Verifies IVoyageReranker is registered as a singleton, the configure callback flows
/// through to the resolved instance, and the SDK's IRerankClient is required.
/// </summary>
public class VoyageRagServiceCollectionExtensionsTests
{
    [Fact]
    public void AddVoyageReranker_NullServices_Throws()
    {
        var act = () => ((IServiceCollection)null!).AddVoyageReranker();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddVoyageReranker_RegistersSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IVoyageReranker>();
        var second = provider.GetRequiredService<IVoyageReranker>();

        first.Should().BeSameAs(second); // singleton: same instance across resolutions
        first.Should().BeOfType<VoyageReranker>();
    }

    [Fact]
    public async Task AddVoyageReranker_NoCallback_UsesDefaults()
    {
        using var provider = BuildProvider();

        var reranker = (VoyageReranker)provider.GetRequiredService<IVoyageReranker>();

        // We can't directly inspect VoyageReranker's internal options, but the reranker is
        // functional — a round-trip with the fake client confirms defaults were accepted.
        var fake = (FakeRerankClient)provider.GetRequiredService<IRerankClient>();
        var results = await reranker.RerankAsync("q", ["a", "b"]);

        fake.CallCount.Should().Be(1);
        fake.LastRequest!.Model.Should().Be(VoyageAIModels.Rerank2);
        fake.LastRequest.TopK.Should().Be(5);
        fake.LastRequest.Truncation.Should().BeTrue();
    }

    [Fact]
    public async Task AddVoyageReranker_ConfigureCallback_FlowsToResolvedReranker()
    {
        using var provider = BuildProvider(o =>
        {
            o.Model = VoyageAIModels.Rerank2Lite;
            o.TopK = 3;
            o.Truncation = false;
        });

        var reranker = (VoyageReranker)provider.GetRequiredService<IVoyageReranker>();
        var fake = (FakeRerankClient)provider.GetRequiredService<IRerankClient>();

        await reranker.RerankAsync("q", ["a"]);

        fake.LastRequest!.Model.Should().Be(VoyageAIModels.Rerank2Lite);
        fake.LastRequest.TopK.Should().Be(3);
        fake.LastRequest.Truncation.Should().BeFalse();
    }

    [Fact]
    public void AddVoyageReranker_MissingIRerankClient_ThrowsOnResolve()
    {
        // Don't register IRerankClient — the factory should throw.
        var services = new ServiceCollection();
        services.AddVoyageReranker();
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IVoyageReranker>();
        act.Should().Throw<InvalidOperationException>();
    }

    private static ServiceProvider BuildProvider(Action<VoyageRerankerOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRerankClient>(new FakeRerankClient());
        services.AddVoyageReranker(configure);
        return services.BuildServiceProvider();
    }
}
