using VoyageAI.Models;

namespace VoyageAI;

/// <summary>
/// Configuration for <see cref="VoyageReranker"/>. Values apply to every
/// <see cref="IVoyageReranker.RerankAsync"/> call.
/// </summary>
public sealed class VoyageRerankerOptions
{
    /// <summary>
    /// Reranker model id. Defaults to <see cref="VoyageAIModels.Rerank2"/> — Voyage's
    /// high-performance general-purpose reranker (16K context per query+document). Use
    /// <see cref="VoyageAIModels.Rerank2Lite"/> for a faster, lighter alternative.
    /// </summary>
    public string Model { get; set; } = VoyageAIModels.Rerank2;

    /// <summary>
    /// Number of most-relevant documents to return. Omit (<see langword="null"/>) to
    /// return all documents, reranked. Defaults to <c>5</c>, a sensible context-window
    /// size for chat agents.
    /// </summary>
    public int? TopK { get; set; } = 5;

    /// <summary>
    /// When <see langword="true"/> (default), over-length query/documents are truncated
    /// to fit the model context. When <see langword="false"/>, an over-length input
    /// raises an error.
    /// </summary>
    public bool Truncation { get; set; } = true;
}
