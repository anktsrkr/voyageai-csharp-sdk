# VoyageAI.Sdk

Enterprise .NET SDK for the [Voyage AI](https://docs.voyageai.com) REST API — text
embeddings, multimodal (text + image) embeddings, and reranking. Built on
`HttpClientFactory`, Polly v8 resilience, client-side rate limiting, and
`System.Text.Json` source generation (AOT/trim safe).

Targets **.NET 10** and **.NET 8 (LTS)**.

## Installation

```bash
dotnet add package VoyageAI.Sdk
```

## Quick start (ASP.NET Core)

`appsettings.json`:

```json
{
  "VoyageAI": {
    "ApiKey": "pa-...",
    "MaxRetryAttempts": 3,
    "ClientSideRpmLimit": 1900
  }
}
```

`Program.cs`:

```csharp
builder.Services.AddVoyageAI();

public class MyService(IEmbeddingsClient embeddings)
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var response = await embeddings.EmbedAsync(
            new EmbeddingRequest { Model = VoyageAIModels.Voyage3, Input = text },
            ct);
        return response.Data[0].Embedding.ToArray();
    }
}
```

If `ApiKey` is omitted from configuration it is read from the `VOYAGE_API_KEY`
environment variable.

## Quick Start (console / standalone host)

```csharp
var host = Host.CreateApplicationBuilder();
host.Services.AddVoyageAI(opts =>
{
    opts.ApiKey = Environment.GetEnvironmentVariable("VOYAGE_API_KEY")!;
    opts.MaxRetryAttempts = 5;
});
var app = host.Build();
var embeddings = app.Services.GetRequiredService<IEmbeddingsClient>();
```

## Text Embeddings

```csharp
var req = new EmbeddingRequest
{
    Model = VoyageAIModels.Voyage3Large,
    Input = new[] { "I like cats", "I also like dogs" },
    InputType = InputType.Document,
    OutputDimension = 1024,
};
var res = await embeddings.EmbedAsync(req, ct);
foreach (var item in res.Data)
{
    Console.WriteLine($"#{item.Index}: {item.Embedding.Count} dims");
}
```

Pass a single string via the implicit conversion (`Input = "hello"`) or build a batch
explicitly (`EmbeddingInput.From(...)`). The API allows up to 128 inputs per request; the
SDK enforces this client-side.

## Multimodal Embeddings

```csharp
var req = new MultimodalEmbeddingRequest
{
    Model = VoyageAIModels.VoyageMultimodal3,
    Inputs = new[]
    {
        MultimodalInput.From(
            new TextContentPart("This is a banana."),
            new ImageUrlContentPart("https://example.com/banana.jpg")),
    },
};
var res = await multimodal.EmbedAsync(req, ct);
```

Use `ImageBase64ContentPart` with a `data:image/jpeg;base64,...` data URL to send image
bytes directly.

## Reranking

```csharp
var req = new RerankRequest
{
    Model = VoyageAIModels.Rerank2,
    Query = "When was the UN founded?",
    Documents = new[] { "The UN was founded in 1945.", "Cats are mammals." },
    TopK = 1,
    ReturnDocuments = true,
};
var res = await rerank.RerankAsync(req, ct);
foreach (var r in res.Data)
{
    Console.WriteLine($"#{r.Index} score={r.RelevanceScore:0.000}: {r.Document}");
}
```

## Configuration Reference

| Property | Default | Description |
|---|---|---|
| `ApiKey` | *(env fallback)* | Bearer token; falls back to `VOYAGE_API_KEY`. |
| `BaseAddress` | `https://api.voyageai.com/v1/` | API base URL. |
| `RequestTimeout` | `00:01:40` | Per-request HTTP timeout. |
| `MaxRetryAttempts` | `3` | Retries on 429 / 5xx (`0`–`10`). |
| `ClientSideRpmLimit` | `1900` | Client-side requests-per-minute guard. |
| `CircuitBreakerFailureRatio` | `0.5` | 5xx failure ratio that opens the breaker. |
| `CircuitBreakerDuration` | `00:00:30` | How long the breaker stays open. |

## Resilience & Rate Limiting

Two complementary layers (per the design spec):

- **Client-side RPM guard** — a `SlidingWindowRateLimiter` (six 10-second segments) rejects
  outbound traffic above `ClientSideRpmLimit` with `VoyageAIRateLimitException`.
- **Server-side 429 back-off** — the Polly retry pipeline honours the `Retry-After` header
  (delta-seconds or HTTP-date), falling back to exponential backoff with jitter. The
  circuit breaker trips **only** on 5xx, never on 429.

## Error Handling

```
VoyageAIException              (base — catch for any SDK failure)
├── VoyageAIAuthException      (HTTP 401)
├── VoyageAIRateLimitException (HTTP 429 / client guard; exposes RetryAfter)
└── VoyageAIValidationException (HTTP 400 / client-side Guard)
```

```csharp
try { await embeddings.EmbedAsync(req, ct); }
catch (VoyageAIRateLimitException ex) when (ex.RetryAfter is { } wait)
{
    await Task.Delay(wait, ct);
}
```

## AOT & Trimming

All serialization goes through the source-generated `VoyageAIJsonContext`. The project is
`IsAotCompatible` with full trimming on .NET 10; no reflection-based JSON or validation.

## License

MIT — see [LICENSE](https://github.com/voyage-ai/voyage-ai-sdk-dotnet/blob/main/LICENSE).
