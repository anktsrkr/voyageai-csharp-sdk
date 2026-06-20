namespace VoyageAI.Http;

/// <summary>
/// Logical names for the named <see cref="System.Net.Http.HttpClient"/> instances
/// registered by the SDK. One per endpoint so each can be configured independently.
/// </summary>
public static class VoyageAIHttpClientNames
{
    /// <summary>Named client for <c>POST /embeddings</c>.</summary>
    public const string Embeddings = "VoyageAI.Embeddings";

    /// <summary>Named client for <c>POST /multimodalembeddings</c>.</summary>
    public const string MultimodalEmbeddings = "VoyageAI.MultimodalEmbeddings";

    /// <summary>Named client for <c>POST /rerank</c>.</summary>
    public const string Rerank = "VoyageAI.Rerank";
}
