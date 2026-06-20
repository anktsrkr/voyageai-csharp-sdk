using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>Request body for <c>POST /embeddings</c>.</summary>
public sealed record EmbeddingRequest
{
    /// <summary>
    /// A single text string or a list of up to 128 texts. Use the implicit conversions
    /// on <see cref="EmbeddingInput"/> (e.g. <c>Input = "hello"</c> or
    /// <c>Input = new[] { "a", "b" }</c>).
    /// </summary>
    [JsonRequired]
    public required EmbeddingInput Input { get; init; }

    /// <summary>
    /// Model name (e.g. <see cref="VoyageAIModels.Voyage3"/>). See
    /// <see cref="VoyageAIModels"/> for known values.
    /// </summary>
    [JsonRequired]
    public required string Model { get; init; }

    /// <summary>
    /// Whether inputs are queries or documents. Omit (<see langword="null"/>) to embed
    /// verbatim. See <see cref="InputType"/>.
    /// </summary>
    [JsonPropertyName("input_type")]
    public InputType? InputType { get; init; }

    /// <summary>
    /// Truncate over-length inputs to fit context. Default <see langword="true"/>; when
    /// <see langword="false"/>, an over-length input raises an error.
    /// </summary>
    public bool? Truncation { get; init; } = true;

    /// <summary>
    /// Output dimension. Most models support a single default (<see langword="null"/>);
    /// <c>voyage-3-large</c> / <c>voyage-code-3</c> support 2048, 1024 (default), 512, 256.
    /// </summary>
    [JsonPropertyName("output_dimension")]
    public int? OutputDimension { get; init; }

    /// <summary>Numeric dtype of returned vectors. Default <see cref="OutputDtype.Float"/>.</summary>
    [JsonPropertyName("output_dtype")]
    public OutputDtype OutputDtype { get; init; } = OutputDtype.Float;

    /// <summary>
    /// Encoding format. Omit for native JSON arrays; <see cref="EncodingFormat.Base64"/>
    /// returns Base64-encoded NumPy arrays.
    /// </summary>
    [JsonPropertyName("encoding_format")]
    public EncodingFormat? EncodingFormat { get; init; }

    /// <summary>Creates an embedding request for a single string input.</summary>
    public static EmbeddingRequest Create(string model, string input) =>
        new() { Model = model, Input = input };

    /// <summary>Creates an embedding request for a batch of string inputs.</summary>
    public static EmbeddingRequest Create(string model, IReadOnlyList<string> inputs) =>
        new() { Model = model, Input = EmbeddingInput.From(inputs) };
}
