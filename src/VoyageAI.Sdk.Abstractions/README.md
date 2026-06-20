# VoyageAI.Sdk.Abstractions

Contracts-only package for the [Voyage AI](https://docs.voyageai.com) .NET SDK: the client
interfaces (`IEmbeddingsClient`, `IMultimodalEmbeddingsClient`, `IRerankClient`), request
and response models, enums, exceptions, and model-name constants.

Reference this package — instead of the full `VoyageAI.Sdk` runtime — when you only need
the interfaces and DTOs, for example for mocking in tests or for authoring a library that
consumes the SDK without taking a hard dependency on `HttpClient`/Polly.

```bash
dotnet add package VoyageAI.Sdk.Abstractions
```

```csharp
public class MySearchService(IEmbeddingsClient embeddings)
{
    public Task<EmbeddingResponse> EmbedAsync(string text, CancellationToken ct) =>
        embeddings.EmbedAsync(
            new EmbeddingRequest { Model = VoyageAIModels.Voyage3, Input = text },
            ct);
}
```

For the full client runtime (resilience, rate limiting, DI registration), use
[`VoyageAI.Sdk`](https://www.nuget.org/packages/VoyageAI.Sdk) instead.

## License

MIT — see [LICENSE](https://github.com/voyage-ai/voyage-ai-sdk-dotnet/blob/main/LICENSE).
