using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Serialization;

/// <summary>
/// Round-trip tests for <see cref="EmbeddingObjectConverter"/>. The API <c>embedding</c>
/// field arrives as either a JSON array of numbers (native encoding) or a single Base64
/// string (<c>encoding_format = base64</c>); this asserts both branches route to the
/// correct <see cref="EmbeddingObject"/> property, the exactly-one-populated invariant,
/// unknown-property forwarding, and write symmetry.
/// </summary>
public class EmbeddingObjectConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new EmbeddingObjectConverter() },
    };

    // --- Read (deserialize) ---

    [Fact]
    public void Read_NumericArray_PopulatesEmbeddingAndLeavesBase64Null()
    {
        const string json =
            """{ "object": "embedding", "embedding": [0.1, 0.2, 0.3], "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj.Should().NotBeNull();
        obj!.Object.Should().Be("embedding");
        obj.Embedding.Should().Equal(0.1f, 0.2f, 0.3f);
        obj.EmbeddingBase64.Should().BeNull();
        obj.Index.Should().Be(0);
    }

    [Fact]
    public void Read_Base64String_PopulatesEmbeddingBase64AndLeavesArrayEmpty()
    {
        const string json =
            """{ "object": "embedding", "embedding": "AAECAwQ=", "index": 7 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj.Should().NotBeNull();
        obj!.EmbeddingBase64.Should().Be("AAECAwQ=");
        obj.Embedding.Should().BeEmpty();
        obj.Index.Should().Be(7);
    }

    [Fact]
    public void Read_MissingObject_DefaultsToEmbeddingLiteral()
    {
        // The converter defaults Object to "embedding" when absent or null.
        const string json = """{ "embedding": [1.0], "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj!.Object.Should().Be("embedding");
    }

    [Fact]
    public void Read_NullObject_DefaultsToEmbeddingLiteral()
    {
        const string json = """{ "object": null, "embedding": [1.0], "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj!.Object.Should().Be("embedding");
    }

    [Fact]
    public void Read_UnknownProperty_IsForwardCompatibleAndIgnored()
    {
        // Forward compatibility: unknown fields must not break deserialization.
        const string json =
            """{ "object": "embedding", "embedding": [0.5], "index": 1, "future_field": 42, "another": "x" }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj.Should().NotBeNull();
        obj!.Embedding.Should().Equal(0.5f);
        obj.Index.Should().Be(1);
    }

    [Fact]
    public void Read_EmptyArray_ProducesEmptyEmbedding()
    {
        const string json = """{ "embedding": [], "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        obj!.Embedding.Should().BeEmpty();
        obj.EmbeddingBase64.Should().BeNull();
    }

    [Fact]
    public void Read_NonObjectToken_ThrowsJsonException()
    {
        const string json = "[1, 2, 3]";

        var act = () => JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        act.Should().Throw<JsonException>();
    }

    // --- Write (serialize) ---

    [Fact]
    public void Write_NumericEmbedding_ProducesJsonArrayField()
    {
        var obj = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = new[] { 1f, 2f, 3f },
            Index = 0,
        };

        var json = JsonSerializer.Serialize(obj, Options);

        // Order written by the converter: object, embedding, index.
        json.Should().Contain("\"object\":\"embedding\"");
        json.Should().Contain("\"embedding\":[1,2,3]");
        json.Should().Contain("\"index\":0");
        json.Should().NotContain("null");
    }

    [Fact]
    public void Write_Base64Embedding_ProducesStringField()
    {
        var obj = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = Array.Empty<float>(),
            EmbeddingBase64 = "AAECAwQ=",
            Index = 2,
        };

        var json = JsonSerializer.Serialize(obj, Options);

        json.Should().Contain("\"embedding\":\"AAECAwQ=\"");
    }

    // --- Round-trip ---

    [Fact]
    public void RoundTrip_NumericArray_PreservesValues()
    {
        var original = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = new[] { 0.1f, 0.2f, 0.3f },
            Index = 9,
        };

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        roundTripped!.Embedding.Should().Equal(0.1f, 0.2f, 0.3f);
        roundTripped.EmbeddingBase64.Should().BeNull();
        roundTripped.Index.Should().Be(9);
    }

    [Fact]
    public void RoundTrip_Base64_PreservesValue()
    {
        var original = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = Array.Empty<float>(),
            EmbeddingBase64 = "AAECAwQ=",
            Index = 3,
        };

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<EmbeddingObject>(json, Options);

        roundTripped!.EmbeddingBase64.Should().Be("AAECAwQ=");
        roundTripped.Embedding.Should().BeEmpty();
    }

    // --- Exactly-one-populated invariant ---

    [Fact]
    public void NativeEncoding_HasExactlyOnePopulatedProperty()
    {
        const string json = """{ "embedding": [0.1, 0.2], "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options)!;

        (obj.Embedding.Count > 0).Should().BeTrue();
        obj.EmbeddingBase64.Should().BeNull();
    }

    [Fact]
    public void Base64Encoding_HasExactlyOnePopulatedProperty()
    {
        const string json = """{ "embedding": "AAECAwQ=", "index": 0 }""";

        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options)!;

        obj.EmbeddingBase64.Should().NotBeNull();
        obj.Embedding.Should().BeEmpty();
    }

    // --- Enterprise/MongoDB Compatibility Tests ---

    [Fact]
    public void Read_NumericArray_BackingTypeIsFloatArray()
    {
        const string json = """{ "embedding": [1.5, -2.0, 3.25], "index": 0 }""";
        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options)!;

        obj.Embedding.Should().BeOfType<float[]>();
    }

    [Fact]
    public void AsMemory_NumericArray_ReturnsCorrectMemoryWithoutAllocation()
    {
        const string json = """{ "embedding": [1.5, -2.0, 3.25], "index": 0 }""";
        var obj = JsonSerializer.Deserialize<EmbeddingObject>(json, Options)!;

        var memory1 = obj.AsMemory();
        var memory2 = obj.AsMemory();

        memory1.ToArray().Should().Equal(1.5f, -2.0f, 3.25f);
        
        // Assert exact same reference (zero allocations on successive calls and wraps the original array)
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory1, out var segment1).Should().BeTrue();
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory2, out var segment2).Should().BeTrue();
        segment1.Array.Should().BeSameAs(segment2.Array);
        segment1.Array.Should().BeSameAs((float[])obj.Embedding);
    }

    [Fact]
    public void AsMemory_Base64Float_DecodesAndCachesCorrectly()
    {
        var floatBytes = new byte[8];
        Buffer.BlockCopy(new float[] { 1.0f, -2.5f }, 0, floatBytes, 0, 8);
        var base64 = Convert.ToBase64String(floatBytes);

        var obj = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = Array.Empty<float>(),
            EmbeddingBase64 = base64,
            Index = 0
        };

        var memory1 = obj.AsMemory();
        var memory2 = obj.AsMemory();

        memory1.ToArray().Should().Equal(1.0f, -2.5f);

        // Assert exact same reference is cached and returned
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory1, out var segment1).Should().BeTrue();
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory2, out var segment2).Should().BeTrue();
        segment1.Array.Should().BeSameAs(segment2.Array);
    }

    [Fact]
    public void DecodeBase64_QuantizedInt8_DecodesCorrectly()
    {
        // 127 = 0x7F, -128 = 0x80, 42 = 0x2A
        var int8Bytes = new byte[] { 0x7F, 0x80, 0x2A }; 
        var base64 = Convert.ToBase64String(int8Bytes);

        var obj = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = Array.Empty<float>(),
            EmbeddingBase64 = base64,
            Index = 0
        };

        sbyte[] decoded = obj.DecodeBase64<sbyte>();
        decoded.Should().Equal((sbyte)127, (sbyte)-128, (sbyte)42);
    }

    [Fact]
    public async Task AsMemory_MultithreadedAccess_IsThreadSafeAndReturnsSingleReference()
    {
        var floatBytes = new byte[8];
        Buffer.BlockCopy(new float[] { 3.14f, -9.9f }, 0, floatBytes, 0, 8);
        var base64 = Convert.ToBase64String(floatBytes);

        var obj = new EmbeddingObject
        {
            Object = "embedding",
            Embedding = Array.Empty<float>(),
            EmbeddingBase64 = base64,
            Index = 0
        };

        const int ThreadCount = 20;
        var tasks = new Task<ReadOnlyMemory<float>>[ThreadCount];
        
        for (int i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(() => obj.AsMemory());
        }

        var results = await Task.WhenAll(tasks);

        // Verify all threads got the correct values
        foreach (var result in results)
        {
            result.ToArray().Should().Equal(3.14f, -9.9f);
        }

        // Verify all threads got the exact same array instance reference (no race condition/duplicate creation)
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(results[0], out var segment0).Should().BeTrue();
        for (int i = 1; i < ThreadCount; i++)
        {
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray(results[i], out var segmentN).Should().BeTrue();
            segmentN.Array.Should().BeSameAs(segment0.Array);
        }
    }
}
