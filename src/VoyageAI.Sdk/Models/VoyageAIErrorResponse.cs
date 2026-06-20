namespace VoyageAI.Models;

/// <summary>
/// The API's standard error envelope: <c>{ "detail": "..." }</c>. Deserialized only on
/// non-2xx responses to populate exception messages.
/// </summary>
internal sealed record VoyageAIErrorResponse
{
    /// <summary>The human-readable error message from the API.</summary>
    public required string Detail { get; init; }
}
