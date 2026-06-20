namespace VoyageAI.Tests.Unit.MicrosoftExtensionsAI.Helpers;

/// <summary>
/// Builders for the response types whose <c>Object</c> discriminator is a <c>required</c>
/// member (always <c>"list"</c>/<c>"embedding"</c>). Centralizing them keeps the test data
/// terse and self-documenting, mirroring the SDK test project's <c>TestData</c>.
/// </summary>
internal static class TestData
{
    /// <summary>Builds an <see cref="EmbeddingObject"/> with the <c>Object</c> discriminator set.</summary>
    public static EmbeddingObject Embedding(int index, params float[] vector) => new()
    {
        Object = "embedding",
        Index = index,
        Embedding = vector,
    };

    /// <summary>Builds an <see cref="EmbeddingResponse"/> with one embedding per input count.</summary>
    public static EmbeddingResponse EmbeddingResponse(
        string model,
        int totalTokens,
        IReadOnlyList<EmbeddingObject> data) => new()
    {
        Object = "list",
        Model = model,
        Data = data,
        Usage = new UsageInfo { TotalTokens = totalTokens },
    };
}
