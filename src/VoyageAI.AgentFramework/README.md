# VoyageAI.AgentFramework

Microsoft Agent Framework integration for the [Voyage AI .NET SDK](https://github.com/voyage-ai/voyage-ai-sdk-dotnet).

Composes a **search** stage (`IVoyageRagSearcher<T>`) with the **Voyage reranker**
(`IVoyageReranker`) and hands the result to the Agent Framework's `TextSearchProvider`, so
you get two-stage retrieval (vector search → rerank) on any agent with a few lines of wiring.

## Why

The Agent Framework's `TextSearchProvider` is **sealed** and takes a single
`Func<string, CancellationToken, Task<IEnumerable<TextSearchResult>>>` delegate in its
constructor. Building that delegate by hand — embed the query, run a vector search, score
candidates with the reranker, map indices back to records, project to `TextSearchResult` —
is ~80 lines of orchestration that gets copy-pasted into every RAG sample.

This package factors that into a **search/rerank split**: you implement only the
transport-specific search (MongoDB, pgvector, Redis, Qdrant, …), and the package handles the
rerank composition and the three framework wiring paths.

## Install

```
dotnet add package VoyageAI.AgentFramework
```

References `VoyageAI.Sdk`, `Microsoft.Agents.AI`, and `Microsoft.Agents.AI.Abstractions`
(1.10.0).

## Quick start

```csharp
using VoyageAI;

// 1. Implement the search stage (transport-specific — this one knows about MongoDB).
//    Lives in your app, not in this package.
public sealed class MongoSearcher : IVoyageRagSearcher<KBDocument> { ... }

var searcher = new MongoSearcher(mongoCollection, queryEmbedder, limit: 10);

// 2. Build a reranker from the Voyage SDK client.
var reranker = new VoyageReranker(rerankClient, new VoyageRerankerOptions { TopK = 3 });

// 3. Wire RAG into your agent — pick one of the three paths:
```

### `ChatClientAgentOptions` (mutator)

```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "Answer using the provided context; cite sources." }
}.UseVoyageRag(searcher, reranker, o => o.RerankerOptions.TopK = 3));
```

### `AIAgentBuilder` (pipeline)

```csharp
AIAgent agent = chatClient.AsAIAgent()
    .UseVoyageRag(searcher, reranker, o => o.RerankerOptions.TopK = 3);
```

### `ChatClientBuilder` (MEAI pipeline)

```csharp
ChatClientBuilder builder = new(openAiClient)
    .UseVoyageRag(searcher, reranker);
```

### Dependency injection

```csharp
// Registers IVoyageReranker as a singleton. AddVoyageAI must already be registered so
// IRerankClient can be resolved.
builder.Services.AddVoyageAI(o => o.ApiKey = apiKey);
builder.Services.AddVoyageReranker(o => o.Model = VoyageAIModels.Rerank2);
```

## Skipping reranking

Pass `reranker: null` to use the searcher's ordering as-is (single-stage retrieval):

```csharp
chatClient.AsAIAgent().UseVoyageRag(searcher, reranker: null);
```

## Result mapping

By default the provider builds `TextSearchResult` entries with `Text` always set, plus
`SourceName`/`SourceLink` populated **if** your record type implements
`IVoyageSearchResultMetadata`, and `RawRepresentation` set to the record.

For record types that don't fit that shape, supply a custom mapper. The mapper receives a
strongly-typed candidate — no cast, no per-candidate wrapper allocation:

```csharp
.UseVoyageRag(searcher, reranker, o => o.ResultMapper = c => new()
{
    Text = c.Text,
    SourceName = c.Record.Title
});
```

Or skip a searcher class entirely with the ad-hoc delegate factory:

```csharp
var provider = VoyageRagContextProvider.Create<KBDocument>(
    (query, ct) => SearchAsync(query, ct),
    reranker);
```

## Design notes

- **Does not subclass `TextSearchProvider`** — it's sealed; we compose with it.
- **No transport dependency** — the package references only the Voyage SDK and Agent
  Framework abstractions. MongoDB/pgvector/Redis live in your app.
- **Reranking is optional** — `reranker: null` preserves the searcher's order.
- **`RerankerOptions` is non-nullable** — defaults to `new()` so consumers never need `!`.
