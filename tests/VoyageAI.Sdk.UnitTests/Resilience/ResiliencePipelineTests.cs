using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Resilience;

/// <summary>
/// Exercises the standard Polly v8 resilience pipeline (retry + circuit breaker) end to
/// end via the real DI registration (<see cref="TestHost"/>). MockHttp drives deterministic
/// canned responses so retry counts and the breaker's open transition are asserted without
/// timing-based waits (the jittered backoff makes exact-delay assertions unreliable).
/// </summary>
public class ResiliencePipelineTests
{
    /// <summary>A MockHttp responder that counts how many times it fired.</summary>
    private sealed class CountingResponder
    {
        private int _count;
        public int Count => _count;

        public Func<HttpRequestMessage, HttpResponseMessage> Respond(
            HttpStatusCode status, string body, TimeSpan? retryAfter = null)
        {
            return _ =>
            {
                Interlocked.Increment(ref _count);
                var msg = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                if (retryAfter is { } ra)
                {
                    msg.Headers.RetryAfter = new RetryConditionHeaderValue(ra);
                }
                return msg;
            };
        }
    }

    /// <summary>
    /// A responder that returns a different response on each invocation, so a transient
    /// failure can be followed by success.
    /// </summary>
    private sealed class SequenceResponder
    {
        private readonly Queue<Func<HttpResponseMessage>> _queue = new();
        private int _count;
        public int Count => _count;

        public SequenceResponder Then(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
        {
            _queue.Enqueue(() =>
            {
                var msg = new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                if (retryAfter is { } ra)
                {
                    msg.Headers.RetryAfter = new RetryConditionHeaderValue(ra);
                }
                return msg;
            });
            return this;
        }

        public Func<HttpRequestMessage, HttpResponseMessage> Build()
        {
            return req =>
            {
                Interlocked.Increment(ref _count);
                var factory = _queue.Count > 1 ? _queue.Dequeue() : _queue.Peek();
                return factory();
            };
        }
    }

    [Fact]
    public async Task Retry_ThenSuccess_RetriesAndEventuallySucceeds()
    {
        using var host = new TestHost();
        var seq = new SequenceResponder()
            .Then(HttpStatusCode.InternalServerError, """{ "detail": "transient" }""")
            .Then(HttpStatusCode.OK, TestData.EmbeddingsResponseBody);

        host.MockHttp.When("*/embeddings").Respond(req => seq.Build()(req));

        // Keep retries on (default = 3); the first 500 is retried and then succeeds.
        using var provider = host.Build(o =>
        {
            o.ApiKey = "k";
            o.MaxRetryAttempts = 3;
            // Keep the breaker high so a single 500 doesn't open it here.
            o.CircuitBreakerFailureRatio = 1.0;
        });
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var response = await client.EmbedAsync(TestData.SingleEmbeddingRequest());

        response.Data.Should().HaveCount(1);
        seq.Count.Should().Be(2, because: "the initial 500 plus one successful retry");
    }

    [Fact]
    public async Task Retry_After429WithRetryAfterHeader_ThenSuccess()
    {
        using var host = new TestHost();
        var seq = new SequenceResponder()
            .Then(HttpStatusCode.TooManyRequests, """{ "detail": "slow" }""", TimeSpan.FromSeconds(0))
            .Then(HttpStatusCode.OK, TestData.EmbeddingsResponseBody);

        host.MockHttp.When("*/embeddings").Respond(req => seq.Build()(req));

        using var provider = host.Build(o =>
        {
            o.ApiKey = "k";
            o.MaxRetryAttempts = 3;
            o.CircuitBreakerFailureRatio = 1.0;
        });
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var response = await client.EmbedAsync(TestData.SingleEmbeddingRequest());

        response.Data.Should().HaveCount(1);
        seq.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task NonRetriedStatus_ThrowsImmediatelyWithoutRetry()
    {
        using var host = new TestHost();
        var responder = new CountingResponder();
        host.MockHttp
            .When("*/embeddings")
            .Respond(req => responder.Respond(
                HttpStatusCode.BadRequest, TestData.ValidationErrorBody)(req));

        using var provider = host.Build(o =>
        {
            o.ApiKey = "k";
            o.MaxRetryAttempts = 3;
        });
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        (await act.Should().ThrowAsync<VoyageAIValidationException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 400 is not in the retry predicate → exactly one attempt, no retries.
        responder.Count.Should().Be(1);
    }

    /// <summary>
    /// Drives enough 500 responses through the pipeline to exceed the circuit breaker's
    /// MinimumThroughput within its SamplingDuration, opening the circuit. A subsequent
    /// call then short-circuits with <see cref="BrokenCircuitException"/> (or a wrapped
    /// variant). Isolated and asserted via the Polly exception type.
    /// </summary>
    [Fact]
    public async Task CircuitBreaker_OpensAfterConsecutiveFailures_ThenShortCircuits()
    {
        using var host = new TestHost();
        var responder = new CountingResponder();
        host.MockHttp
            .When("*/embeddings")
            .Respond(req => responder.Respond(
                HttpStatusCode.InternalServerError, """{ "detail": "down" }""")(req));

        // Low ratio + default MinimumThroughput=5 within the 30s sampling window: a handful
        // of 500s (each exhausted through retries) is enough to open the breaker.
        using var provider = host.Build(o =>
        {
            o.ApiKey = "k";
            o.MaxRetryAttempts = 1;   // minimum valid value; each call fails after one retry
            o.CircuitBreakerFailureRatio = 0.1;
            o.CircuitBreakerDuration = TimeSpan.FromSeconds(30);
        });
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        // Fire failing calls until the breaker opens (MinimumThroughput=5 sampled failures).
        // The circuit may open mid-loop, after which calls throw BrokenCircuitException
        // rather than a VoyageAIException — catch broadly here and only assert the open
        // state afterwards.
        var opened = false;
        for (var i = 0; i < 10 && !opened; i++)
        {
            try
            {
                await client.EmbedAsync(TestData.SingleEmbeddingRequest());
            }
            catch (VoyageAIException)
            {
                // A 500 failure — feeds the breaker's sampled failure count.
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("BrokenCircuit"))
            {
                opened = true;
            }
        }

        // The circuit should now be open (either observed above, or via this final call).
        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        var thrown = await act.Should().ThrowAsync<Exception>();
        // BrokenCircuitException lives in Polly; assert by name to avoid a hard type ref.
        thrown.Which.GetType().FullName.Should().Contain("BrokenCircuit",
            because: "an open circuit short-circuits with Polly's BrokenCircuitException");

        // And no further HTTP attempt was made for the short-circuited call beyond the
        // failures already counted (the responder count did not grow significantly).
        responder.Count.Should().BeLessThanOrEqualTo(8);
    }
}
