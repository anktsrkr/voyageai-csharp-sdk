namespace VoyageAI.Tests.Unit.Helpers;

/// <summary>
/// A <see cref="DelegatingHandler"/> whose <see cref="SendAsync"/> never completes until
/// the gated <see cref="Release"/> <see cref="TaskCompletionSource"/> is set. Used by the
/// <see cref="RateLimitHandler"/> tests to hold acquired/queued requests in flight so a
/// subsequent overflow request deterministically fails the client-side guard — no timing
/// races, no sleeps.
/// </summary>
internal sealed class BlockingHandler : DelegatingHandler
{
    private readonly TaskCompletionSource _tcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The number of requests that have entered the pipeline and are blocked.</summary>
    public int EnteredCount => _entered;

    private int _entered;

    /// <summary>Releases every blocked request, letting them complete.</summary>
    public void Release() => _tcs.TrySetResult();

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _entered);
        await _tcs.Task.ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
