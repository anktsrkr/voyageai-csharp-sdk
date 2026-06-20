using Microsoft.Agents.AI;

namespace VoyageAI;

/// <summary>
/// RAG wiring extensions for <see cref="ChatClientAgentOptions"/>, the options bag passed
/// to <c>IChatClient.AsAIAgent(ChatClientAgentOptions)</c>. Appends a Voyage-composed
/// <see cref="TextSearchProvider"/> to <see cref="ChatClientAgentOptions.AIContextProviders"/>.
/// </summary>
public static class ChatClientAgentOptionsExtensions
{
    /// <summary>
    /// Appends a Voyage RAG context provider (search + optional rerank) to the options'
    /// <see cref="ChatClientAgentOptions.AIContextProviders"/> list and returns the same
    /// options instance for fluent composition.
    /// </summary>
    /// <param name="options">The agent options bag to mutate.</param>
    /// <param name="searcher">The transport-specific search stage.</param>
    /// <param name="reranker">The optional Voyage rerank stage. <see langword="null"/>
    /// skips reranking and uses the searcher's ordering as-is.</param>
    /// <param name="configure">Optional callback to set provider options (rerank config,
    /// result mapper, text-search options).</param>
    public static ChatClientAgentOptions UseVoyageRag<T>(
        this ChatClientAgentOptions options,
        IVoyageRagSearcher<T> searcher,
        IVoyageReranker? reranker = null,
        Action<VoyageRagContextProviderOptions<T>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var providerOptions = new VoyageRagContextProviderOptions<T>();
        configure?.Invoke(providerOptions);

        TextSearchProvider provider =
            VoyageRagContextProvider.Create(searcher, reranker, providerOptions);

        // AIContextProviders is exposed as IEnumerable<AIContextProvider> with a public
        // setter and a null default, so we coalesce any existing providers, append ours,
        // and reassign — preserving providers the caller may have already added.
        List<AIContextProvider> providers = options.AIContextProviders is null
            ? []
            : [..options.AIContextProviders];
        providers.Add(provider);
        options.AIContextProviders = providers;
        return options;
    }
}
