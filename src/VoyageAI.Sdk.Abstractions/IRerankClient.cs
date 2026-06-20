using VoyageAI.Models;

namespace VoyageAI;

/// <summary>Client for the Voyage AI rerank endpoint (<c>POST /rerank</c>).</summary>
public interface IRerankClient
{
    /// <summary>
    /// Reranks the request's <see cref="RerankRequest.Documents"/> against the query.
    /// </summary>
    /// <param name="request">Validated rerank request.</param>
    /// <param name="cancellationToken">Propagated to the HTTP call and retry pipeline.</param>
    /// <returns>The rerank response, with results sorted by descending relevance score.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="VoyageAIValidationException">The request fails client-side validation.</exception>
    /// <exception cref="VoyageAIAuthException">The API key is invalid (HTTP 401).</exception>
    /// <exception cref="VoyageAIRateLimitException">Rate limited after exhausting retries (HTTP 429).</exception>
    /// <exception cref="VoyageAIException">Any other API or transport failure.</exception>
    Task<RerankResponse> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default);
}
