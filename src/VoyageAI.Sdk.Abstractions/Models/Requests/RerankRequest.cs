using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>Request body for <c>POST /rerank</c>.</summary>
public sealed record RerankRequest
{
    /// <summary>The query string.</summary>
    [JsonRequired]
    public required string Query { get; init; }

    /// <summary>The documents to rerank (max 1000).</summary>
    [JsonRequired]
    public required IReadOnlyList<string> Documents { get; init; }

    /// <summary>Reranker model name (e.g. <see cref="VoyageAIModels.Rerank2"/>).</summary>
    [JsonRequired]
    public required string Model { get; init; }

    /// <summary>
    /// Number of most-relevant documents to return. Omit (<see langword="null"/>) to
    /// return all documents, reranked.
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; init; }

    /// <summary>Whether to echo the document text in the response. Default <see langword="false"/>.</summary>
    [JsonPropertyName("return_documents")]
    public bool ReturnDocuments { get; init; }

    /// <summary>
    /// Truncate query/documents to satisfy context-length limits. Default <see langword="true"/>.
    /// </summary>
    public bool Truncation { get; init; } = true;

    /// <summary>Creates a rerank request.</summary>
    public static RerankRequest Create(
        string model, string query, IReadOnlyList<string> documents) =>
        new() { Model = model, Query = query, Documents = documents };
}
