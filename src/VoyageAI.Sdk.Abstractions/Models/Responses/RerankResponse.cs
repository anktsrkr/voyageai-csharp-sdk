namespace VoyageAI.Models;

/// <summary>Response body from <c>POST /rerank</c>.</summary>
public sealed record RerankResponse
{
    /// <summary>The object type, always <c>"list"</c>.</summary>
    public required string Object { get; init; } = "list";

    /// <summary>Reranking results sorted by descending <see cref="RerankResult.RelevanceScore"/>.</summary>
    public required IReadOnlyList<RerankResult> Data { get; init; } = Array.Empty<RerankResult>();

    /// <summary>Name of the model that produced the reranking.</summary>
    public required string Model { get; init; }

    /// <summary>Token usage for the request.</summary>
    public required UsageInfo Usage { get; init; } = new();
}
