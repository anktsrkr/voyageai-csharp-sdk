using VoyageAI.Models;

namespace VoyageAI;

/// <summary>Client for the Voyage AI multimodal embeddings endpoint (<c>POST /multimodalembeddings</c>).</summary>
public interface IMultimodalEmbeddingsClient
{
    /// <summary>
    /// Embeds the request's multimodal <see cref="MultimodalEmbeddingRequest.Inputs"/> (text, images, or both).
    /// </summary>
    /// <param name="request">Validated multimodal embedding request.</param>
    /// <param name="cancellationToken">Propagated to the HTTP call and retry pipeline.</param>
    /// <returns>The embedding response with one vector per input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="VoyageAIValidationException">The request fails client-side validation.</exception>
    /// <exception cref="VoyageAIAuthException">The API key is invalid (HTTP 401).</exception>
    /// <exception cref="VoyageAIRateLimitException">Rate limited after exhausting retries (HTTP 429).</exception>
    /// <exception cref="VoyageAIException">Any other API or transport failure.</exception>
    Task<MultimodalEmbeddingResponse> EmbedAsync(
        MultimodalEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
