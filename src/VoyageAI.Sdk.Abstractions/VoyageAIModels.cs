namespace VoyageAI;

/// <summary>
/// Known Voyage AI model identifiers. Exposed as <see cref="string"/> constants rather
/// than an enum so newly released models can be passed as string literals without a
/// library update.
/// </summary>
public static class VoyageAIModels
{
    // ───────────────────────── Text embeddings ─────────────────────────
    /// <summary>Large embedding model, 32K context, 1024 dims (default; also 2048/512/256).</summary>
    public const string Voyage3Large = "voyage-3-large";

    /// <summary>General-purpose embedding model, 32K context, 1024 dims.</summary>
    public const string Voyage3 = "voyage-3";

    /// <summary>Lightweight embedding model, 32K context, 512 dims.</summary>
    public const string Voyage3Lite = "voyage-3-lite";

    /// <summary>Embedding model optimized for code retrieval, 32K context, 1024 dims.</summary>
    public const string VoyageCode3 = "voyage-code-3";

    /// <summary>Embedding model optimized for finance, 32K context, 1024 dims.</summary>
    public const string VoyageFinance2 = "voyage-finance-2";

    /// <summary>Embedding model optimized for legal text, 32K context, 1024 dims.</summary>
    public const string VoyageLaw2 = "voyage-law-2";

    // ───────────────────────── Multimodal ─────────────────────────
    /// <summary>Multimodal (text + image) embedding model, 32K context, 1024 dims.</summary>
    public const string VoyageMultimodal3 = "voyage-multimodal-3";

    // ───────────────────────── Rerankers ─────────────────────────
    /// <summary>Latest general-purpose reranker (16K context per query+document).</summary>
    public const string Rerank2 = "rerank-2";

    /// <summary>Lightweight, faster reranker (8K context per query+document).</summary>
    public const string Rerank2Lite = "rerank-2-lite";

    /// <summary>Previous-generation reranker.</summary>
    public const string Rerank1 = "rerank-1";

    /// <summary>Previous-generation lightweight reranker.</summary>
    public const string RerankLite1 = "rerank-lite-1";
}
