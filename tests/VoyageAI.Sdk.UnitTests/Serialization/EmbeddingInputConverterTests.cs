using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Serialization;

/// <summary>
/// Round-trip tests for <see cref="EmbeddingInputConverter"/>: single-string ↔ bare JSON
/// string and batch ↔ JSON array, plus the <see cref="EmbeddingInput"/> factory methods,
/// implicit conversions, and the <see cref="EmbeddingInput.IsBatch"/>/<see cref="EmbeddingInput.Count"/>
/// accessors. Uses <see cref="JsonSerializer"/> directly with the converter attached.
/// </summary>
public class EmbeddingInputConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new EmbeddingInputConverter() },
    };

    // --- Write (serialize) ---

    [Fact]
    public void Write_SingleInput_ProducesBareJsonString()
    {
        var input = EmbeddingInput.From("hello");

        var json = JsonSerializer.Serialize(input, Options);

        json.Should().Be("\"hello\"");
    }

    [Fact]
    public void Write_BatchInput_ProducesJsonArrayOfStrings()
    {
        var input = EmbeddingInput.From("a", "b", "c");

        var json = JsonSerializer.Serialize(input, Options);

        json.Should().Be("[\"a\",\"b\",\"c\"]");
    }

    // --- Read (deserialize) ---

    [Fact]
    public void Read_BareString_ProducesSingleInput()
    {
        var input = JsonSerializer.Deserialize<EmbeddingInput>("\"world\"", Options);

        input.Should().NotBeNull();
        input!.IsBatch.Should().BeFalse();
        input.Single.Should().Be("world");
        input.Batch.Should().BeNull();
    }

    [Fact]
    public void Read_JsonArray_ProducesBatchInput()
    {
        var input = JsonSerializer.Deserialize<EmbeddingInput>("[\"x\",\"y\"]", Options);

        input.Should().NotBeNull();
        input!.IsBatch.Should().BeTrue();
        input.Batch.Should().Equal("x", "y");
        input.Single.Should().BeNull();
    }

    [Theory]
    [InlineData("123")]                       // number → invalid
    [InlineData("true")]                      // bool → invalid
    public void Read_InvalidToken_ThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<EmbeddingInput>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Read_ArrayWithNonStringElement_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<EmbeddingInput>("[1,2]", Options);

        var ex = act.Should().Throw<JsonException>().Which;
        ex.Message.Should().Contain("string elements");
    }

    // --- Round-trip ---

    [Fact]
    public void RoundTrip_Single_PreservesValue()
    {
        var original = EmbeddingInput.From("round-trip-me");

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<EmbeddingInput>(json, Options);

        roundTripped!.IsBatch.Should().BeFalse();
        roundTripped.Single.Should().Be("round-trip-me");
    }

    [Fact]
    public void RoundTrip_Batch_PreservesValues()
    {
        var original = EmbeddingInput.From("one", "two", "three");

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<EmbeddingInput>(json, Options);

        roundTripped!.IsBatch.Should().BeTrue();
        roundTripped.Batch.Should().Equal("one", "two", "three");
    }

    // --- Factories & accessors ---

    [Fact]
    public void From_SingleString_CreatesNonBatchInput()
    {
        var input = EmbeddingInput.From("only");

        input.IsBatch.Should().BeFalse();
        input.Count.Should().Be(1);
        input.Single.Should().Be("only");
    }

    [Fact]
    public void From_ParamsArray_CreatesBatchInput()
    {
        var input = EmbeddingInput.From("a", "b");

        input.IsBatch.Should().BeTrue();
        input.Count.Should().Be(2);
        input.Batch.Should().Equal("a", "b");
    }

    [Fact]
    public void From_Enumerable_MaterializesBatch()
    {
        IEnumerable<string> Lazy() { yield return "x"; yield return "y"; }

        var input = EmbeddingInput.From(Lazy());

        input.IsBatch.Should().BeTrue();
        input.Count.Should().Be(2);
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesSingleInput()
    {
        EmbeddingInput input = "via-implicit";

        input.IsBatch.Should().BeFalse();
        input.Single.Should().Be("via-implicit");
    }

    [Fact]
    public void ImplicitConversion_FromStringArray_CreatesBatchInput()
    {
        EmbeddingInput input = new[] { "a", "b" };

        input.IsBatch.Should().BeTrue();
        input.Batch.Should().Equal("a", "b");
    }

    [Fact]
    public void ImplicitConversion_FromListOfStrings_CreatesBatchInput()
    {
        EmbeddingInput input = new List<string> { "a", "b" };

        input.IsBatch.Should().BeTrue();
        input.Batch.Should().Equal("a", "b");
    }

    [Fact]
    public void Count_ReflectsBatchLength()
    {
        EmbeddingInput.From("a", "b", "c", "d").Count.Should().Be(4);
        EmbeddingInput.From("solo").Count.Should().Be(1);
    }

    [Fact]
    public void ToString_IndicatesShape()
    {
        EmbeddingInput.From("solo").ToString().Should().Contain("Single");
        EmbeddingInput.From("a", "b").ToString().Should().Contain("Batch");
        EmbeddingInput.From("a", "b").ToString().Should().Contain("2");
    }
}
