using Microsoft.Extensions.Logging;

namespace VoyageAI.Tests.Unit.Helpers;

/// <summary>
/// A dependency-free <see cref="ILogger{TCategoryName}"/> spy that records every
/// <see cref="ILogger.Log{TState}"/> call. The source-generated <see cref="LoggerMessage"/>
/// calls in the SDK flow through <see cref="ILogger.Log"/> (not the extension methods), so
/// this captures the structured <c>(EventId, LogLevel, message)</c> tuples cleanly.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly object _gate = new();

    /// <summary>The captured log entries in invocation order.</summary>
    public List<LogEntry> Entries { get; } = new();

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            Entries.Add(new LogEntry(
                eventId,
                logLevel,
                formatter(state, exception),
                exception));
        }
    }

    /// <summary>One captured log record.</summary>
    public sealed record LogEntry(EventId EventId, LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
