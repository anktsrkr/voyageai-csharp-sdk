using VoyageAI.Configuration;

namespace VoyageAI.Http;

/// <summary>
/// Client-side requests-per-minute guard. Wraps every outgoing request in a
/// <see cref="SlidingWindowRateLimiter"/> (six 10-second segments over a 1-minute
/// window) so the SDK never exceeds the configured <see cref="VoyageAIOptions.ClientSideRpmLimit"/>
/// regardless of server-side limits. Brief bursting is permitted via the lease queue;
/// once the queue is full, requests fail fast with <see cref="VoyageAIRateLimitException"/>.
/// </summary>
internal sealed class RateLimitHandler : DelegatingHandler, IDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;

    /// <summary>Initializes a new <see cref="RateLimitHandler"/>.</summary>
    public RateLimitHandler(IOptionsMonitor<VoyageAIOptions> options)
    {
        var rpm = options.CurrentValue.ClientSideRpmLimit;
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rpm,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,                          // 10-second segments
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = rpm * 2,                           // permit brief bursting queue
        });
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Acquire one permit per request; release on dispose (the `using` scope).
        using var lease = await _limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            throw new VoyageAIRateLimitException(
                "Client-side rate limit exceeded. Reduce request throughput or raise " +
                $"{nameof(VoyageAIOptions.ClientSideRpmLimit)}.");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _limiter.Dispose();
        }
        base.Dispose(disposing);
    }
}
