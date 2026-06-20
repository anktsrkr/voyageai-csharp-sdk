using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>Request body for <c>POST /multimodalembeddings</c>.</summary>
public sealed record MultimodalEmbeddingRequest
{
    /// <summary>The multimodal inputs to vectorize (max 1000).</summary>
    [JsonRequired]
    public required IReadOnlyList<MultimodalInput> Inputs { get; init; }

    /// <summary>Model name. Currently only <see cref="VoyageAIModels.VoyageMultimodal3"/> is supported.</summary>
    [JsonRequired]
    public required string Model { get; init; }

    /// <summary>Whether inputs are queries or documents. Omit to embed verbatim.</summary>
    [JsonPropertyName("input_type")]
    public InputType? InputType { get; init; }

    /// <summary>Truncate over-length inputs to fit context. Default <see langword="true"/>.</summary>
    public bool? Truncation { get; init; } = true;

    /// <summary>
    /// Encoding format. Omit for native JSON float arrays; <see cref="EncodingFormat.Base64"/>
    /// returns Base64-encoded NumPy arrays.
    /// </summary>
    [JsonPropertyName("output_encoding")]
    public EncodingFormat? OutputEncoding { get; init; }
}
