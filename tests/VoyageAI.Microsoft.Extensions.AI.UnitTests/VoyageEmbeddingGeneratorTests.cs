namespace VoyageAI.Tests.Unit.MicrosoftExtensionsAI;

/// <summary>
/// Coverage for <see cref="VoyageEmbeddingGenerator"/>: request mapping (single/batch/
/// overrides/defaults), response mapping (vectors, usage, additional properties), the
/// <see cref="VoyageEmbeddingGenerator.GetService"/> surface, dispose semantics, and the
/// empty-input short-circuit. Uses <see cref="FakeEmbeddingsClient"/> so no HTTP pipeline
/// is involved — the generator under test only depends on the interface.
/// </summary>
public class VoyageEmbeddingGeneratorTests
{
    // ───────────────────────── Request mapping ─────────────────────────

    [Fact]
    public async Task GenerateAsync_SingleInput_MapsModelAndInputAndReturnsOneEmbedding()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync(["hello world"]);

        client.CallCount.Should().Be(1);
        var request = client.LastRequest!;
        request.Model.Should().Be(VoyageAIModels.Voyage3);
        // The generator materializes every input set into a list and sends it as a batch
        // (a single-element batch is still a valid request), so a one-element input becomes
        // a batch of 1 rather than a Single input.
        request.Input.IsBatch.Should().BeTrue();
        request.Input.Batch.Should().Equal(["hello world"]);
        request.InputType.Should().Be(InputType.Document); // default

        result.Should().HaveCount(1);
        result[0].Vector.ToArray().Should().Equal(0f, 0f, 0f); // FakeEmbeddingsClient index 0 → all zeros
    }

    [Fact]
    public async Task GenerateAsync_BatchInput_MapsToBatchRequestAndPreservesOrder()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync(["a", "b", "c"]);

        var request = client.LastRequest!;
        request.Input.IsBatch.Should().BeTrue();
        request.Input.Batch.Should().Equal(["a", "b", "c"]);

        // Results are ordered by index; the fake fills index i with 0.1i/0.2i/0.3i so we can
        // confirm ordering survived the generator's OrderBy(d => d.Index) mapping.
        result.Should().HaveCount(3);
        result[0].Vector.ToArray().Should().Equal(0f, 0f, 0f);
        result[1].Vector.ToArray().Should().Equal(0.1f, 0.2f, 0.3f);
        result[2].Vector.ToArray().Should().Equal(0.2f, 0.4f, 0.6f);
    }

    [Fact]
    public async Task GenerateAsync_BatchInput_OrdersResultsByIndexEvenWhenApiReturnsOutOfOrder()
    {
        // The API contract is one EmbeddingObject per input indexed 0..N-1, but the wire
        // order is not guaranteed. The generator sorts by index before mapping so callers
        // always get inputs back in submission order.
        var client = new FakeEmbeddingsClient
        {
            NextResponse = TestData.EmbeddingResponse(
                VoyageAIModels.Voyage3,
                3,
                new[]
                {
                    TestData.Embedding(2, 2f),
                    TestData.Embedding(0, 0f),
                    TestData.Embedding(1, 1f),
                }),
        };
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync(["a", "b", "c"]);

        result[0].Vector.ToArray().Should().Equal(0f);
        result[1].Vector.ToArray().Should().Equal(1f);
        result[2].Vector.ToArray().Should().Equal(2f);
    }

    [Fact]
    public async Task GenerateAsync_EmptyInput_ReturnsEmptyCollectionAndDoesNotCallApi()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync([]);

        result.Should().BeEmpty();
        client.CallCount.Should().Be(0); // short-circuit: no API call for empty input
    }

    [Fact]
    public async Task GenerateAsync_DefaultOptions_WhenNullOptionsPassed_UsesVoyage3AndDocument()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        await generator.GenerateAsync(["x"], options: null);

        var request = client.LastRequest!;
        request.Model.Should().Be(VoyageAIModels.Voyage3);
        request.InputType.Should().Be(InputType.Document);
        request.Truncation.Should().Be(true);
    }

    [Fact]
    public async Task GenerateAsync_PerCallModelId_OverridesOptionsModel()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            Model = VoyageAIModels.Voyage3Lite,
        });

        await generator.GenerateAsync(["x"], new EmbeddingGenerationOptions
        {
            ModelId = VoyageAIModels.VoyageCode3,
        });

        client.LastRequest!.Model.Should().Be(VoyageAIModels.VoyageCode3);
    }

    [Fact]
    public async Task GenerateAsync_PerCallDimensions_OverridesOptionsOutputDimension()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            OutputDimension = 256,
        });

        await generator.GenerateAsync(["x"], new EmbeddingGenerationOptions
        {
            Dimensions = 512,
        });

        client.LastRequest!.OutputDimension.Should().Be(512);
    }

    [Fact]
    public async Task GenerateAsync_NoPerCallDimensions_FallsBackToOptionsOutputDimension()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            OutputDimension = 1024,
        });

        await generator.GenerateAsync(["x"]);

        client.LastRequest!.OutputDimension.Should().Be(1024);
    }

    [Fact]
    public async Task GenerateAsync_BlankModelId_FallsBackToOptionsModel()
    {
        // Whitespace-only ModelId should not override — a blank string is not a model id.
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            Model = VoyageAIModels.Voyage3Large,
        });

        await generator.GenerateAsync(["x"], new EmbeddingGenerationOptions { ModelId = "   " });

        client.LastRequest!.Model.Should().Be(VoyageAIModels.Voyage3Large);
    }

    [Fact]
    public async Task GenerateAsync_PassesTruncationAndInputTypeFromOptions()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            InputType = InputType.Query,
            Truncation = false,
        });

        await generator.GenerateAsync(["x"]);

        var request = client.LastRequest!;
        request.InputType.Should().Be(InputType.Query);
        request.Truncation.Should().Be(false);
    }

    [Fact]
    public async Task GenerateAsync_PropagatesCancellationTokenToClient()
    {
        var client = new FakeEmbeddingsClient
        {
            NextException = new OperationCanceledException(),
        };
        var generator = new VoyageEmbeddingGenerator(client);

        using var cts = new CancellationTokenSource();
        var act = () => generator.GenerateAsync(["x"], cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ───────────────────────── Response mapping ─────────────────────────

    [Fact]
    public async Task GenerateAsync_MapsUsageTokensToInputAndTotalCounts()
    {
        var client = new FakeEmbeddingsClient
        {
            NextResponse = TestData.EmbeddingResponse(
                VoyageAIModels.Voyage3,
                42,
                new[] { TestData.Embedding(0, 1f) }),
        };
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync(["x"]);

        // Voyage reports a single combined total; map to both input + total (no output tokens).
        result.Usage.Should().NotBeNull();
        result.Usage!.InputTokenCount.Should().Be(42);
        result.Usage.TotalTokenCount.Should().Be(42);
        result.Usage.OutputTokenCount.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_StoresResponseModelInAdditionalProperties()
    {
        var client = new FakeEmbeddingsClient
        {
            NextResponse = TestData.EmbeddingResponse(
                VoyageAIModels.Voyage3Lite,
                0,
                new[] { TestData.Embedding(0, 1f) }),
        };
        var generator = new VoyageEmbeddingGenerator(client);

        var result = await generator.GenerateAsync(["x"]);

        result.AdditionalProperties.Should().NotBeNull();
        result.AdditionalProperties!.ContainsKey(nameof(EmbeddingResponse.Model)).Should().BeTrue();
        result.AdditionalProperties[nameof(EmbeddingResponse.Model)].Should().Be(VoyageAIModels.Voyage3Lite);
    }

    // ───────────────────────── GetService ─────────────────────────

    [Fact]
    public void GetService_ForGeneratorType_ReturnsThis()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        generator.GetService(typeof(VoyageEmbeddingGenerator)).Should().BeSameAs(generator);
    }

    [Fact]
    public void GetService_ForEmbeddingsClientInterface_ReturnsInnerClient()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        generator.GetService(typeof(IEmbeddingsClient)).Should().BeSameAs(client);
    }

    [Fact]
    public void GetService_ForMetadata_ReturnsMetadataWithDefaultsFromOptions()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
        {
            Model = VoyageAIModels.Voyage3Large,
            OutputDimension = 1024,
        });

        var metadata = generator.GetService(typeof(EmbeddingGeneratorMetadata))
            as EmbeddingGeneratorMetadata;

        metadata.Should().NotBeNull();
        metadata!.ProviderName.Should().Be("voyageai");
        metadata.DefaultModelId.Should().Be(VoyageAIModels.Voyage3Large);
        metadata.DefaultModelDimensions.Should().Be(1024);
    }

    [Fact]
    public void GetService_ForUnknownType_ReturnsNull()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        generator.GetService(typeof(string)).Should().BeNull();
    }

    // ───────────────────────── Construction & dispose ─────────────────────────

    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        var act = () => new VoyageEmbeddingGenerator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client, options: null);

        var metadata = generator.GetService(typeof(EmbeddingGeneratorMetadata))
            as EmbeddingGeneratorMetadata;

        metadata!.DefaultModelId.Should().Be(VoyageAIModels.Voyage3);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // The generator is a no-op dispose (the client is owned by the DI container), so this
        // just locks in that contract: it must not throw and must remain idempotent.
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        var act = () =>
        {
            generator.Dispose();
            generator.Dispose(); // idempotent
        };

        act.Should().NotThrow();
    }

    // ───────────────────────── Argument validation ─────────────────────────

    [Fact]
    public async Task GenerateAsync_NullValues_ThrowsArgumentNullException()
    {
        var client = new FakeEmbeddingsClient();
        var generator = new VoyageEmbeddingGenerator(client);

        var act = () => generator.GenerateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
