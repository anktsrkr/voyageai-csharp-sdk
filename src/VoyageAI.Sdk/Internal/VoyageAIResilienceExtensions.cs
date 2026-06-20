using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using VoyageAI.Configuration;

namespace VoyageAI.Internal;

/// <summary>
/// Builds the standard Polly v8 resilience pipeline applied to every Voyage AI named
/// <see cref="HttpClient"/>: total timeout → retry (exponential backoff + jitter,
/// honouring the <c>Retry-After</c> header on 429) → circuit breaker (5xx only) →
/// per-attempt timeout.
/// </summary>
internal static class VoyageAIResilienceExtensions
{
    /// <summary>The options-instance key the resilience handler reads at build time.</summary>
    public const string PipelineKey = "voyage-standard";

    /// <summary>
    /// Adds the standard resilience pipeline to an <see cref="IHttpClientBuilder"/>. The
    /// pipeline re-reads <see cref="VoyageAIOptions"/> on each reload via
    /// <see cref="IOptionsMonitor{TOptions}"/> so runtime changes take effect.
    /// </summary>
    public static IHttpClientBuilder AddVoyageResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler(PipelineKey, static (pipelineBuilder, ctx) =>
        {
            var opts = ctx.ServiceProvider
                .GetRequiredService<IOptionsMonitor<VoyageAIOptions>>()
                .CurrentValue;

            // 1. Total timeout (outermost) — twice the per-request timeout so a full retry
            //    sequence can still complete.
            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(opts.RequestTimeout.TotalSeconds * 2));

            // 2. Retry — transient HTTP statuses + transport errors. Respects the server's
            //    Retry-After header when present (overrides the computed backoff).
            var retryPredicate = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => r.StatusCode is HttpStatusCode.TooManyRequests
                                   or HttpStatusCode.InternalServerError
                                   or HttpStatusCode.BadGateway
                                   or HttpStatusCode.ServiceUnavailable
                                   or HttpStatusCode.GatewayTimeout);

            pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = opts.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(60),
                ShouldHandle = retryPredicate,
                DelayGenerator = static args =>
                {
                    // Honour Retry-After (delta seconds or HTTP-date) when the server sent one.
                    if (args.Outcome.Result?.Headers?.RetryAfter is { } retryAfter)
                    {
                        var delay = retryAfter.Delta
                            ?? (retryAfter.Date.HasValue
                                ? retryAfter.Date.Value - DateTimeOffset.UtcNow
                                : null);
                        if (delay.HasValue && delay.Value > TimeSpan.Zero)
                        {
                            return new ValueTask<TimeSpan?>(delay);
                        }
                    }
                    return new ValueTask<TimeSpan?>((TimeSpan?)null);
                },
            });

            // 3. Circuit breaker — opens on a high 5xx failure ratio. Excludes 429 so
            //    rate-limited traffic never trips the breaker (per §8 of the design spec).
            var breakerPredicate = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode >= 500);

            pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = opts.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = opts.CircuitBreakerDuration,
                ShouldHandle = breakerPredicate,
            });

            // 4. Per-attempt timeout (innermost).
            pipelineBuilder.AddTimeout(opts.RequestTimeout);
        });

        return builder;
    }
}
