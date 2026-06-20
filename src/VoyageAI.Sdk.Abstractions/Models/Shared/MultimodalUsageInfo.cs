using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>Token and pixel usage for a multimodal embeddings response.</summary>
public sealed record MultimodalUsageInfo
{
    /// <summary>Total text tokens across all inputs.</summary>
    [JsonPropertyName("text_tokens")]
    public int TextTokens { get; init; }

    /// <summary>Total image pixels across all inputs.</summary>
    [JsonPropertyName("image_pixels")]
    public int ImagePixels { get; init; }

    /// <summary>Combined total of text and image tokens (every 560 pixels = 1 token).</summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
