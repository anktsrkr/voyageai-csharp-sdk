using System.Net;

namespace VoyageAI;

/// <summary>
/// Raised when the API returns HTTP 429 and the retry pipeline is exhausted, or when
/// the client-side RPM guard rejects a request. See <see cref="RetryAfter"/> for the
/// server's recommended back-off window, when available.
/// </summary>
public sealed class VoyageAIRateLimitException : VoyageAIException
{
    /// <summary>Initializes a new <see cref="VoyageAIRateLimitException"/>.</summary>
    public VoyageAIRateLimitException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.TooManyRequests;
    }

    /// <summary>The delay the server advised via the <c>Retry-After</c> header, when present.</summary>
    public TimeSpan? RetryAfter { get; init; }
}
