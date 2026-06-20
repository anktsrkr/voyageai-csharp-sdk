namespace VoyageAI.Tests.Unit.MicrosoftExtensionsAI;

/// <summary>
/// DI registration tests for <see cref="VoyageAIEmbeddingGeneratorExtensions.AddVoyageEmbeddingGenerator"/>.
/// Verifies the generator is registered as a singleton <see cref="IEmbeddingGenerator{TValue, TEmbedding}"/>,
/// the options callback flows through to a resolved generator, and the SDK's typed clients
/// resolve when <c>AddVoyageAI</c> is called first (the generator depends on them; it does not
/// register them itself).
/// </summary>
public class VoyageAIEmbeddingGeneratorExtensionsTests
{
    [Fact]
    public void AddVoyageEmbeddingGenerator_RegistersEmbeddingGeneratorAsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var second = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        first.Should().BeSameAs(second); // Singleton: same instance across resolutions.
        first.Should().BeOfType<VoyageEmbeddingGenerator>();
    }

    [Fact]
    public void AddVoyageEmbeddingGenerator_RegistersUnderlyingVoyageClients()
    {
        // BuildProvider calls AddVoyageAI first; the typed client registered there must resolve.
        using var provider = BuildProvider();

        provider.GetService<IEmbeddingsClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddVoyageEmbeddingGenerator_WithoutAddVoyageAI_ThrowsOnResolve()
    {
        // The generator no longer bootstraps the transport itself: it resolves
        // IEmbeddingsClient at activation, so a missing AddVoyageAI fails loudly with a clear
        // "no service for type IEmbeddingsClient" error rather than silently registering infra.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVoyageEmbeddingGenerator();

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IEmbeddingsClient*");
    }

    [Fact]
    public void AddVoyageEmbeddingGenerator_OptionsCallback_FlowsToResolvedGenerator()
    {
        using var provider = BuildProvider(opts =>
        {
            opts.Model = VoyageAIModels.VoyageCode3;
            opts.OutputDimension = 512;
        });

        var generator = (VoyageEmbeddingGenerator)provider
            .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var metadata = generator.GetService(typeof(EmbeddingGeneratorMetadata))
            as EmbeddingGeneratorMetadata;

        metadata!.DefaultModelId.Should().Be(VoyageAIModels.VoyageCode3);
        metadata.DefaultModelDimensions.Should().Be(512);
    }

    [Fact]
    public void AddVoyageEmbeddingGenerator_NoCallback_UsesDefaults()
    {
        using var provider = BuildProvider();

        var generator = (VoyageEmbeddingGenerator)provider
            .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var metadata = generator.GetService(typeof(EmbeddingGeneratorMetadata))
            as EmbeddingGeneratorMetadata;

        metadata!.DefaultModelId.Should().Be(VoyageAIModels.Voyage3);
        metadata.DefaultModelDimensions.Should().BeNull();
    }

    [Fact]
    public async Task AddVoyageEmbeddingGenerator_ResolvedGenerator_GeneratesThroughPipeline()
    {
        // End-to-end through DI: the generator pulls IEmbeddingsClient from the container.
        // With no live endpoint the call would fail at the transport layer, so we swap in a
        // fake client registration to prove the wiring is correct without any HTTP call.
        var fake = new FakeEmbeddingsClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVoyageAI(o => o.ApiKey = "test-key");
        services.AddVoyageEmbeddingGenerator();
        // Replace the typed client registration with the fake so no HTTP call is made.
        OverrideTypedClient(services, fake);

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var result = await generator.GenerateAsync(["di-test"]);

        result.Should().HaveCount(1);
        fake.LastRequest!.Model.Should().Be(VoyageAIModels.Voyage3);
    }

    /// <summary>
    /// Removes the SDK's <see cref="IEmbeddingsClient"/> registration (HttpClientFactory
    /// registers it as a singleton with the concrete client type) and re-adds the fake.
    /// Done by descriptor filtering because <c>RemoveAll&lt;T&gt;</c> lives in a separate
    /// package; this keeps the test dependencies minimal.
    /// </summary>
    private static void OverrideTypedClient(IServiceCollection services, IEmbeddingsClient fake)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IEmbeddingsClient))
            {
                services.RemoveAt(i);
            }
        }
        services.AddSingleton(fake);
    }

    private static ServiceProvider BuildProvider(Action<VoyageEmbeddingGeneratorOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVoyageAI(o => o.ApiKey = "test-key");
        services.AddVoyageEmbeddingGenerator(configure);
        return services.BuildServiceProvider();
    }
}
