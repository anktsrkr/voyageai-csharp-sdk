namespace VoyageAI.Models;

/// <summary>Response body from <c>POST /embeddings</c>.</summary>
public sealed record EmbeddingResponse
{
    /// <summary>The object type, always <c>"list"</c>.</summary>
    public required string Object { get; init; } = "list";

    /// <summary>The embedding objects, one per input string.</summary>
    public required IReadOnlyList<EmbeddingObject> Data { get; init; } = Array.Empty<EmbeddingObject>();

    /// <summary>Name of the model that produced the embeddings.</summary>
    public required string Model { get; init; }

    /// <summary>Token usage for the request.</summary>
    public required UsageInfo Usage { get; init; } = new();
}
