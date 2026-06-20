using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// A single rerank result. Entries are sorted by descending
/// <see cref="RelevanceScore"/>. <see cref="Document"/> is populated only when the
/// request set <c>return_documents = true</c>.
/// </summary>
public sealed record RerankResult
{
    /// <summary>The index of the document within the original request list.</summary>
    public int Index { get; init; }

    /// <summary>Relevance score of the document with respect to the query.</summary>
    [JsonPropertyName("relevance_score")]
    public float RelevanceScore { get; init; }

    /// <summary>The original document string. Present only when <c>return_documents = true</c>.</summary>
    public string? Document { get; init; }
}
