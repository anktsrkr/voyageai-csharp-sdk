using System.Text.Json;
using System.Text.Json.Serialization;
using VoyageAI.Models;

namespace VoyageAI.Serialization;

/// <summary>
/// AOT/trim-safe <see cref="JsonSerializerContext"/> for all request/response types.
/// Every (de)serialization call in the SDK goes through <c>VoyageAIJsonContext.Default</c>
/// with a typed <c>JsonSerializer.JsonTypeInfo&lt;T&gt;</c> — never reflection-based
/// <c>JsonSerializer.Serialize(object)</c>.
/// </summary>
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(MultimodalEmbeddingRequest))]
[JsonSerializable(typeof(MultimodalEmbeddingResponse))]
[JsonSerializable(typeof(RerankRequest))]
[JsonSerializable(typeof(RerankResponse))]
[JsonSerializable(typeof(VoyageAIErrorResponse))]
// Polymorphic derived types must be registered so the trimmer preserves them.
[JsonSerializable(typeof(TextContentPart))]
[JsonSerializable(typeof(ImageUrlContentPart))]
[JsonSerializable(typeof(ImageBase64ContentPart))]
[JsonSerializable(typeof(List<TextContentPart>))]
[JsonSerializable(typeof(List<ImageUrlContentPart>))]
[JsonSerializable(typeof(List<ImageBase64ContentPart>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
internal sealed partial class VoyageAIJsonContext : JsonSerializerContext
{
}
