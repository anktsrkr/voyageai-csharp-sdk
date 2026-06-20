using System.Text.Json;
using VoyageAI.Models;
using VoyageAI.Serialization;
using FluentAssertions;
using Xunit;

namespace VoyageAI.Tests.Unit.Serialization;

public class EnumSerializationTests
{
    [Fact]
    public void InputType_SerializesToLowercase()
    {
        var requestQuery = new EmbeddingRequest
        {
            Model = "voyage-3",
            Input = "hello",
            InputType = InputType.Query
        };

        var requestDocument = new EmbeddingRequest
        {
            Model = "voyage-3",
            Input = "hello",
            InputType = InputType.Document
        };

        var jsonQuery = JsonSerializer.Serialize(requestQuery, VoyageAIJsonContext.Default.EmbeddingRequest);
        var jsonDocument = JsonSerializer.Serialize(requestDocument, VoyageAIJsonContext.Default.EmbeddingRequest);

        jsonQuery.Should().Contain("\"input_type\":\"query\"");
        jsonDocument.Should().Contain("\"input_type\":\"document\"");
    }

    [Fact]
    public void OutputDtype_SerializesToLowercase()
    {
        var dtypes = new[]
        {
            (OutputDtype.Float, "float"),
            (OutputDtype.Int8, "int8"),
            (OutputDtype.UInt8, "uint8"),
            (OutputDtype.Binary, "binary"),
            (OutputDtype.UBinary, "ubinary")
        };

        foreach (var (dtype, expectedString) in dtypes)
        {
            var request = new EmbeddingRequest
            {
                Model = "voyage-3",
                Input = "hello",
                OutputDtype = dtype
            };

            var json = JsonSerializer.Serialize(request, VoyageAIJsonContext.Default.EmbeddingRequest);
            json.Should().Contain($"\"output_dtype\":\"{expectedString}\"");
        }
    }

    [Fact]
    public void EncodingFormat_SerializesToLowercase()
    {
        var request = new EmbeddingRequest
        {
            Model = "voyage-3",
            Input = "hello",
            EncodingFormat = EncodingFormat.Base64
        };

        var json = JsonSerializer.Serialize(request, VoyageAIJsonContext.Default.EmbeddingRequest);
        json.Should().Contain("\"encoding_format\":\"base64\"");
    }
}
