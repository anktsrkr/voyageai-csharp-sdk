namespace VoyageAI.Tests.Unit.Helpers;

/// <summary>
/// Canned request/response payloads for the three endpoints, hand-built to match the
/// snake_case wire format the source-generated <see cref="VoyageAIJsonContext"/> produces.
/// Keeping them as constants makes the tests' expected-vs-actual assertions self-documenting.
/// </summary>
internal static class TestData
{
    public const string ApiKey = "test-api-key";

    public const string Model = "voyage-3";
    public const string MultimodalModel = "voyage-multimodal-3";
    public const string RerankModel = "rerank-2";

    /// <summary>A minimal valid embeddings response (one input → one 4-dim vector).</summary>
    public const string EmbeddingsResponseBody =
        """
        {
          "object": "list",
          "data": [
            {
              "object": "embedding",
              "embedding": [0.1, 0.2, 0.3, 0.4],
              "index": 0
            }
          ],
          "model": "voyage-3",
          "usage": { "total_tokens": 5 }
        }
        """;

    /// <summary>Embeddings response using base64 encoding_format.</summary>
    public const string EmbeddingsBase64ResponseBody =
        """
        {
          "object": "list",
          "data": [
            {
              "object": "embedding",
              "embedding": "AAECAwQ=",
              "index": 0
            }
          ],
          "model": "voyage-3",
          "usage": { "total_tokens": 5 }
        }
        """;

    /// <summary>A minimal valid multimodal embeddings response.</summary>
    public const string MultimodalResponseBody =
        """
        {
          "object": "list",
          "data": [
            {
              "object": "embedding",
              "embedding": [0.5, 0.6, 0.7, 0.8],
              "index": 0
            }
          ],
          "model": "voyage-multimodal-3",
          "usage": { "text_tokens": 4, "image_pixels": 0, "total_tokens": 4 }
        }
        """;

    /// <summary>A minimal valid rerank response (two docs, sorted by relevance desc).</summary>
    public const string RerankResponseBody =
        """
        {
          "object": "list",
          "data": [
            { "index": 1, "relevance_score": 0.95, "document": "second doc" },
            { "index": 0, "relevance_score": 0.12, "document": "first doc" }
          ],
          "model": "rerank-2",
          "usage": { "total_tokens": 20 }
        }
        """;

    /// <summary>The API's standard error envelope: <c>{ "detail": "..." }</c>.</summary>
    public const string ValidationErrorBody =
        """{ "detail": "Input is invalid: model not found" }""";

    /// <summary>Builds a single-text embedding request.</summary>
    public static EmbeddingRequest SingleEmbeddingRequest() =>
        new() { Model = Model, Input = "hello world" };

    /// <summary>Builds a batch embedding request.</summary>
    public static EmbeddingRequest BatchEmbeddingRequest(int count) =>
        new() { Model = Model, Input = EmbeddingInput.From(Enumerable.Range(0, count).Select(i => $"doc {i}")) };

    /// <summary>Builds a single-text multimodal embedding request.</summary>
    public static MultimodalEmbeddingRequest MultimodalRequest() =>
        new()
        {
            Model = MultimodalModel,
            Inputs = new[]
            {
                MultimodalInput.From(new TextContentPart("hello image world")),
            },
        };

    /// <summary>Builds a rerank request.</summary>
    public static RerankRequest RerankRequest() =>
        new()
        {
            Model = RerankModel,
            Query = "what is ai?",
            Documents = new[] { "first doc", "second doc" },
            ReturnDocuments = true,
        };

    /// <summary>JSON content for a non-2xx error body.</summary>
    public static StringContent ErrorBody(string detail) =>
        new($"{{ \"detail\": \"{detail}\" }}", Encoding.UTF8, MediaTypeNames.Application.Json);

    /// <summary>JSON content for a success body.</summary>
    public static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, MediaTypeNames.Application.Json);
}
