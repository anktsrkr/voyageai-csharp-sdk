using VoyageAI.Models;

namespace VoyageAI;

/// <summary>Client for the Voyage AI text embeddings endpoint (<c>POST /embeddings</c>).</summary>
public interface IEmbeddingsClient
{
    /// <summary>
    /// Embeds the request's <see cref="EmbeddingRequest.Input"/> using the specified model.
    /// </summary>
    /// <param name="request">Validated embedding request.</param>
    /// <param name="cancellationToken">Propagated to the HTTP call and retry pipeline.</param>
    /// <returns>The embedding response with one vector per input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="VoyageAIValidationException">The request fails client-side validation.</exception>
    /// <exception cref="VoyageAIAuthException">The API key is invalid (HTTP 401).</exception>
    /// <exception cref="VoyageAIRateLimitException">Rate limited after exhausting retries (HTTP 429).</exception>
    /// <exception cref="VoyageAIException">Any other API or transport failure.</exception>
    Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
