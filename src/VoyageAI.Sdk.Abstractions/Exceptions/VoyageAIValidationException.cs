using System.Net;

namespace VoyageAI;

/// <summary>
/// Raised when the API rejects a request with HTTP 400 (Bad Request) — a malformed or
/// invalid request body — or when client-side <c>Guard</c> validation fails before the
/// call is dispatched.
/// </summary>
public sealed class VoyageAIValidationException : VoyageAIException
{
    /// <summary>Initializes a new <see cref="VoyageAIValidationException"/>.</summary>
    public VoyageAIValidationException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }

    /// <summary>Initializes a new <see cref="VoyageAIValidationException"/> with an inner exception.</summary>
    public VoyageAIValidationException(string message, Exception inner) : base(message, inner)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}
