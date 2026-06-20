# VoyageAI.Microsoft.Extensions.AI

[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/ai-extensions) integration
for the [Voyage AI .NET SDK](../VoyageAI.Sdk). Implements
`IEmbeddingGenerator<string, Embedding<float>>` over Voyage text embeddings, so Voyage plugs
into the **standard .NET AI composition model** instead of a parallel abstraction.

This is the keystone package for product integrations: once Voyage speaks
`IEmbeddingGenerator`, the MongoDB driver's native auto-embedding, `Microsoft.Extensions.VectorData`,
Semantic Kernel, and MEAI's caching/batching/logging/OpenTelemetry middleware all work with
no per-integration embed code.

Targets **.NET 10** and **.NET 8 (LTS)**, AOT/trim safe on .NET 10.

## Installation

```bash
dotnet add package VoyageAI.Microsoft.Extensions.AI
```

## Quick start (DI + middleware composition)

`Program.cs`:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Registers the Voyage SDK + an IEmbeddingGenerator over it, and returns a builder so
// the standard MEAI middleware composes on top.
builder.Services.AddVoyageEmbeddingGenerator(opts =>
{
    opts.Model = VoyageAIModels.Voyage3Large;
    opts.OutputDimension = 1024;
})
.UseOpenTelemetry();   // optional: trace every embedding call

var app = builder.Build();
var generator = app.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

GeneratedEmbeddings<Embedding<float>> result =
    await generator.GenerateAsync(["I like cats", "I like dogs"]);
Console.WriteLine($"{result.Count} embeddings, {result.Usage.TotalTokenCount} tokens");
```

API key is resolved the same way as the core SDK: the `"VoyageAI:ApiKey"` configuration
section, falling back to the `VOYAGE_API_KEY` environment variable.

## Standalone (no DI)

```csharp
IEmbeddingsClient client = ...; // resolved from the Voyage SDK
var generator = new VoyageEmbeddingGenerator(client, new VoyageEmbeddingGeneratorOptions
{
    Model = VoyageAIModels.Voyage3,
    InputType = InputType.Document,
});

Embedding<float> single = (await generator.GenerateAsync(["store hiking boots"]))[0];
Console.WriteLine($"{single.Vector.Length} dims");
```

## Options reference

| Property | Default | Description |
|---|---|---|
| `Model` | `voyage-3` | Default model id; overridden per-call by `EmbeddingGenerationOptions.ModelId`. |
| `InputType` | `Document` | Voyage query/document hint. Set `Query` for a search-time generator. `null` embeds verbatim. |
| `OutputDimension` | `null` | Mapped from `EmbeddingGenerationOptions.Dimensions` when supplied; otherwise this default. |
| `Truncation` | `true` | Truncate over-length inputs; `false` raises on over-length. |

## How mappings work

`Microsoft.Extensions.AI` → Voyage request:

| MEAI | Voyage |
|---|---|
| `EmbeddingGenerationOptions.ModelId` | `EmbeddingRequest.Model` (falls back to `Options.Model`) |
| `EmbeddingGenerationOptions.Dimensions` | `EmbeddingRequest.OutputDimension` |
| `Options.InputType` | `EmbeddingRequest.InputType` |
| batch of N inputs | single `EmbedAsync` call with N inputs |

Voyage response → MEAI:

| Voyage | MEAI |
|---|---|
| `EmbeddingObject.AsMemory()` | `Embedding<float>.Vector` |
| `UsageInfo.TotalTokens` | `UsageDetails.InputTokenCount` + `TotalTokenCount` |

## Getting the raw client back

`GetService` exposes the underlying client so components that need non-embedding capabilities
(rerank, multimodal) can reach them:

```csharp
if (generator.GetService(typeof(IEmbeddingsClient)) is IEmbeddingsClient raw)
{
    // use raw directly
}
```

## License

MIT — see [LICENSE](https://github.com/voyage-ai/voyage-ai-sdk-dotnet/blob/main/LICENSE).
