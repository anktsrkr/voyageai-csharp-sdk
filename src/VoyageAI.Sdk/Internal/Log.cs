using Microsoft.Extensions.Logging;

namespace VoyageAI.Internal;

/// <summary>
/// Compile-time source-generated log messages for hot paths. Avoids runtime string
/// formatting/boxing and guarantees structured field names. Sensitive data (API keys,
/// request bodies) is never logged — only endpoint, model, status code, attempt, and
/// retry metadata.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "VoyageAI {Endpoint} request. Model={Model}")]
    public static partial void RequestSent(ILogger logger, string endpoint, string model);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "VoyageAI 429 received. Model={Model}. Attempt={Attempt}. RetryAfter={RetryAfterSeconds}s")]
    public static partial void RateLimitHit(
        ILogger logger, string model, int attempt, double? retryAfterSeconds);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error,
        Message = "VoyageAI request failed. StatusCode={StatusCode}. Detail={Detail}")]
    public static partial void RequestFailed(ILogger logger, int statusCode, string detail);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "VoyageAI circuit breaker opened. Will retry after {BreakDuration}")]
    public static partial void CircuitOpened(ILogger logger, TimeSpan breakDuration);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug,
        Message = "VoyageAI response received. Endpoint={Endpoint}. StatusCode={StatusCode}. ElapsedMs={ElapsedMs}")]
    public static partial void ResponseReceived(
        ILogger logger, string endpoint, int statusCode, long elapsedMs);
}
