using VoyageAI.Models;

namespace VoyageAI;

/// <summary>
/// Configuration for <see cref="VoyageEmbeddingGenerator"/>. Values apply to every
/// <c>GenerateAsync</c> call unless overridden per-call via
/// <see cref="Microsoft.Extensions.AI.EmbeddingGenerationOptions"/>.
/// </summary>
/// <remarks>
/// The Microsoft.Extensions.AI abstraction has no concept of Voyage's
/// <see cref="InputType"/> (query vs. document), but Voyage quality depends on it. This
/// class exposes it as a generator-wide default; call sites that always embed queries
/// (e.g. a search-time query embedder) should set <see cref="InputType"/> to
/// <see cref="VoyageAI.Models.InputType.Query"/>, while indexing pipelines leave it at
/// the <see cref="VoyageAI.Models.InputType.Document"/> default.
/// </remarks>
public sealed class VoyageEmbeddingGeneratorOptions
{
    /// <summary>
    /// Default model id used when the caller does not supply
    /// <see cref="Microsoft.Extensions.AI.EmbeddingGenerationOptions.ModelId"/>.
    /// Defaults to <see cref="VoyageAIModels.Voyage3"/>.
    /// </summary>
    public string Model { get; set; } = VoyageAIModels.Voyage3;

    /// <summary>
    /// Whether inputs are queries or documents. Defaults to
    /// <see cref="VoyageAI.Models.InputType.Document"/>, which is correct for indexing.
    /// Set to <see cref="VoyageAI.Models.InputType.Query"/> for a generator dedicated to
    /// embedding search queries.
    /// </summary>
    /// <remarks>
    /// Set to <see langword="null"/> to embed verbatim (omit <c>input_type</c>).
    /// </remarks>
    public InputType? InputType { get; set; } = VoyageAI.Models.InputType.Document;

    /// <summary>
    /// Default output dimension, mapped to
    /// <see cref="Microsoft.Extensions.AI.EmbeddingGenerationOptions.Dimensions"/> when
    /// the caller supplies it. Leave <see langword="null"/> for the model's default.
    /// Supported on <c>voyage-3-large</c> / <c>voyage-code-3</c> (2048, 1024, 512, 256).
    /// </summary>
    public int? OutputDimension { get; set; }

    /// <summary>
    /// When <see langword="true"/> (default), over-length inputs are truncated to fit the
    /// model context. When <see langword="false"/>, an over-length input raises an error.
    /// </summary>
    public bool? Truncation { get; set; } = true;
}
