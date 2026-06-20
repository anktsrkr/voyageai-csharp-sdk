using Microsoft.Agents.AI;

namespace VoyageAI;

/// <summary>
/// RAG wiring extensions for <see cref="AIAgentBuilder"/>, the builder pipeline returned
/// by <c>IChatClient.AsAIAgent()</c>. Wraps the agent with a Voyage-composed
/// <see cref="TextSearchProvider"/> via <see cref="AIAgentBuilder.UseAIContextProviders"/>.
/// </summary>
public static class AIAgentBuilderExtensions
{
    /// <summary>
    /// Adds a Voyage RAG context provider (search + optional rerank) to the builder
    /// pipeline and returns the same builder for fluent composition.
    /// </summary>
    /// <param name="builder">The agent builder pipeline.</param>
    /// <param name="searcher">The transport-specific search stage.</param>
    /// <param name="reranker">The optional Voyage rerank stage. <see langword="null"/>
    /// skips reranking and uses the searcher's ordering as-is.</param>
    /// <param name="configure">Optional callback to set provider options (rerank config,
    /// result mapper, text-search options).</param>
    public static AIAgentBuilder UseVoyageRag<T>(
        this AIAgentBuilder builder,
        IVoyageRagSearcher<T> searcher,
        IVoyageReranker? reranker = null,
        Action<VoyageRagContextProviderOptions<T>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var providerOptions = new VoyageRagContextProviderOptions<T>();
        configure?.Invoke(providerOptions);

        TextSearchProvider provider =
            VoyageRagContextProvider.Create(searcher, reranker, providerOptions);

        // TextSearchProvider extends MessageAIContextProvider, so it satisfies the
        // params array directly — no adapter required.
        return builder.UseAIContextProviders(provider);
    }
}
