using System.Text.Json;
using System.Text.Json.Serialization;
using VoyageAI.Models;

namespace VoyageAI.Serialization;

/// <summary>
/// Serializes an <see cref="EmbeddingInput"/> as either a bare JSON string or a JSON
/// array of strings, depending on whether it wraps a single value or a batch, and
/// reads either form back. AOT-safe: no reflection. Public (not internal) so the
/// source-generated <c>JsonSerializerContext</c> in the main SDK assembly can
/// instantiate it across assembly boundaries.
/// </summary>
public sealed class EmbeddingInputConverter : JsonConverter<EmbeddingInput>
{
    /// <inheritdoc/>
    public override EmbeddingInput? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new EmbeddingInput(reader.GetString()!);

            case JsonTokenType.StartArray:
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException(
                            $"Expected string elements in embedding input array, found {reader.TokenType}.");
                    }
                    list.Add(reader.GetString()!);
                }
                return new EmbeddingInput(list);

            default:
                throw new JsonException(
                    $"Expected string or array for embedding input, found {reader.TokenType}.");
        }
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, EmbeddingInput value, JsonSerializerOptions options)
    {
        if (value.IsBatch)
        {
            writer.WriteStartArray();
            foreach (var item in value.Batch!)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStringValue(value.Single);
        }
    }
}
