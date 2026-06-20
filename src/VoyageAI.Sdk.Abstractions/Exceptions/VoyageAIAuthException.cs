using System.Net;

namespace VoyageAI;

/// <summary>
/// The API rejected the request with HTTP 401 (Unauthorized) — an invalid or missing
/// API key. Check the <c>VOYAGE_API_KEY</c> environment variable or the SDK options.
/// </summary>
public sealed class VoyageAIAuthException : VoyageAIException
{
    /// <summary>Initializes a new <see cref="VoyageAIAuthException"/>.</summary>
    public VoyageAIAuthException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.Unauthorized;
    }
}
