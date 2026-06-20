using System.Net;

namespace VoyageAI;

/// <summary>
/// Base type for all errors raised by the Voyage AI SDK. Catch this to handle any SDK
/// failure; catch the derived types (<see cref="VoyageAIAuthException"/>,
/// <see cref="VoyageAIRateLimitException"/>, <see cref="VoyageAIValidationException"/>)
/// for category-specific handling.
/// </summary>
public class VoyageAIException : Exception
{
    /// <summary>Initializes a new <see cref="VoyageAIException"/> with a message.</summary>
    public VoyageAIException(string message) : base(message) { }

    /// <summary>Initializes a new <see cref="VoyageAIException"/> with a message and inner exception.</summary>
    public VoyageAIException(string message, Exception? inner) : base(message, inner) { }

    /// <summary>The HTTP status code returned by the API, when applicable.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>The machine-readable <c>detail</c> string from the API error body, when present.</summary>
    public string? ApiDetail { get; init; }
}
