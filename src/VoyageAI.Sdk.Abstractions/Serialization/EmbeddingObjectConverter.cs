using System.Text.Json;
using System.Text.Json.Serialization;
using VoyageAI.Models;

namespace VoyageAI.Serialization;

/// <summary>
/// Reads/writes an <see cref="EmbeddingObject"/>. The API <c>embedding</c> field is a
/// JSON array of numbers for native encoding, or a single Base64 string when
/// <c>encoding_format</c> is <c>base64</c>; this converter routes the token to
/// <see cref="EmbeddingObject.Embedding"/> or <see cref="EmbeddingObject.EmbeddingBase64"/>
/// respectively. AOT-safe: no reflection.
/// </summary>
/// <remarks>
/// Public (not internal) so the source-generated <c>JsonSerializerContext</c> in the
/// main SDK assembly can instantiate it across assembly boundaries.
/// </remarks>
public sealed class EmbeddingObjectConverter : JsonConverter<EmbeddingObject>
{
    private const string ObjectField = "object";
    private const string EmbeddingField = "embedding";
    private const string IndexField = "index";

    /// <inheritdoc/>
    public override EmbeddingObject? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"Expected start of embedding object, found {reader.TokenType}.");
        }

        string? obj = null;
        IReadOnlyList<float> embedding = Array.Empty<float>();
        string? embeddingBase64 = null;
        int index = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException(
                    $"Expected property name in embedding object, found {reader.TokenType}.");
            }

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case ObjectField:
                    obj = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;

                case EmbeddingField:
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            // Base64-encoded NumPy array (encoding_format = base64).
                            embeddingBase64 = reader.GetString();
                            break;

                        case JsonTokenType.StartArray:
                            var floats = new List<float>();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                floats.Add(reader.GetSingle());
                            }
                            embedding = floats.ToArray();
                            break;

                        default:
                            throw new JsonException(
                                $"Expected array or string for embedding field, found {reader.TokenType}.");
                    }
                    break;

                case IndexField:
                    index = reader.GetInt32();
                    break;

                default:
                    // Forward-compatible: skip unknown properties.
                    reader.Skip();
                    break;
            }
        }

        return new EmbeddingObject
        {
            Object = obj ?? "embedding",
            Embedding = embedding,
            EmbeddingBase64 = embeddingBase64,
            Index = index,
        };
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, EmbeddingObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(ObjectField, value.Object);

        writer.WritePropertyName(EmbeddingField);
        if (value.EmbeddingBase64 is not null)
        {
            writer.WriteStringValue(value.EmbeddingBase64);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var f in value.Embedding)
            {
                writer.WriteNumberValue(f);
            }
            writer.WriteEndArray();
        }

        writer.WriteNumber(IndexField, value.Index);
        writer.WriteEndObject();
    }
}
