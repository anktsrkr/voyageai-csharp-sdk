using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// Token usage for an embeddings or rerank response. For multimodal responses see
/// <see cref="MultimodalUsageInfo"/>.
/// </summary>
public sealed record UsageInfo
{
    /// <summary>Total tokens used to compute the result.</summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
