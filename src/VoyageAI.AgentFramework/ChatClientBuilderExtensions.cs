using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace VoyageAI;

/// <summary>
/// RAG wiring extensions for <see cref="ChatClientBuilder"/>, the Microsoft.Extensions.AI
/// client-builder pipeline. Wraps the chat client with a Voyage-composed
/// <see cref="TextSearchProvider"/> via
/// <see cref="AIContextProviderChatClientBuilderExtensions.UseAIContextProviders"/>.
/// </summary>
public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// Adds a Voyage RAG context provider (search + optional rerank) to the chat-client
    /// builder pipeline and returns the same builder for fluent composition.
    /// </summary>
    /// <param name="builder">The chat-client builder pipeline.</param>
    /// <param name="searcher">The transport-specific search stage.</param>
    /// <param name="reranker">The optional Voyage rerank stage. <see langword="null"/>
    /// skips reranking and uses the searcher's ordering as-is.</param>
    /// <param name="configure">Optional callback to set provider options (rerank config,
    /// result mapper, text-search options).</param>
    public static ChatClientBuilder UseVoyageRag<T>(
        this ChatClientBuilder builder,
        IVoyageRagSearcher<T> searcher,
        IVoyageReranker? reranker = null,
        Action<VoyageRagContextProviderOptions<T>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var providerOptions = new VoyageRagContextProviderOptions<T>();
        configure?.Invoke(providerOptions);

        TextSearchProvider provider =
            VoyageRagContextProvider.Create(searcher, reranker, providerOptions);

        // UseAIContextProviders lives on the Agent Framework's ChatClientBuilder extensions
        // (namespace Microsoft.Extensions.AI) and accepts a params AIContextProvider[].
        // TextSearchProvider extends AIContextProvider via MessageAIContextProvider.
        return builder.UseAIContextProviders(provider);
    }
}
