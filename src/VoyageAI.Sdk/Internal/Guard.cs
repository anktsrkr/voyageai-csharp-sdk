using VoyageAI.Models;

namespace VoyageAI.Internal;

/// <summary>
/// Client-side request validation. These checks throw before the network call,
/// avoiding wasted retries and producing clearer errors than a 400 from the API.
/// </summary>
internal static class Guard
{
    /// <summary>Maximum number of embedding inputs per request (API contract).</summary>
    public const int MaxEmbeddingBatchSize = 128;

    /// <summary>Maximum number of rerank documents per request (API contract).</summary>
    public const int MaxRerankDocuments = 1_000;

    /// <summary>Maximum number of multimodal inputs per request (API contract).</summary>
    public const int MaxMultimodalInputs = 1_000;

    /// <summary>Validates the embedding input batch size (max 128).</summary>
    public static void ValidateBatchSize(EmbeddingInput input, int maxBatch = MaxEmbeddingBatchSize)
    {
        if (input.IsBatch && input.Batch!.Count > maxBatch)
        {
            throw new VoyageAIValidationException(
                $"Embedding batch size {input.Batch.Count} exceeds the maximum of {maxBatch}.");
        }
    }

    /// <summary>Validates the rerank document count (max 1000).</summary>
    public static void ValidateDocuments(IReadOnlyList<string> documents, int max = MaxRerankDocuments)
    {
        if (documents.Count > max)
        {
            throw new VoyageAIValidationException(
                $"Rerank document count {documents.Count} exceeds the maximum of {max}.");
        }
    }

    /// <summary>Validates the multimodal input count (max 1000).</summary>
    public static void ValidateMultimodalInputs(
        IReadOnlyList<MultimodalInput> inputs, int max = MaxMultimodalInputs)
    {
        if (inputs.Count > max)
        {
            throw new VoyageAIValidationException(
                $"Multimodal input count {inputs.Count} exceeds the maximum of {max}.");
        }
    }
}
