using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Clients;

/// <summary>
/// Full error-matrix coverage for <see cref="EmbeddingsClient"/>, the canonical client.
/// The status→exception mapping lives in the shared <see cref="VoyageAIBaseClient.PostAsync"/>
/// switch, so it is exercised once here comprehensively rather than duplicated across all
/// three endpoints. Each test goes through the real DI pipeline (auth + rate-limit handlers).
/// </summary>
public class EmbeddingsClientTests
{
    [Fact]
    public async Task EmbedAsync_HappyPath_DeserializesResponseAndSendsBearerToken()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond("application/json", TestData.EmbeddingsResponseBody);

        using var provider = host.Build();
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var response = await client.EmbedAsync(TestData.SingleEmbeddingRequest());

        response.Data.Should().HaveCount(1);
        response.Data[0].Embedding.Should().Equal(0.1f, 0.2f, 0.3f, 0.4f);
        response.Data[0].Index.Should().Be(0);
        response.Model.Should().Be("voyage-3");
        response.Usage.TotalTokens.Should().Be(5);
    }

    [Fact]
    public async Task EmbedAsync_HappyPath_SendsAuthorizationBearerHeader()
    {
        using var host = new TestHost();
        HttpRequestMessage? captured = null;
        host.MockHttp.When("*/embeddings")
            .Respond(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        TestData.EmbeddingsResponseBody, Encoding.UTF8, "application/json"),
                };
            });

        using var provider = host.Build(o => o.ApiKey = "explicit-key-123");
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        await client.EmbedAsync(TestData.SingleEmbeddingRequest());

        captured.Should().NotBeNull();
        captured!.Headers.Authorization.Should().NotBeNull();
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("explicit-key-123");
    }

    [Fact]
    public async Task EmbedAsync_400_ThrowsValidationExceptionWithApiDetail()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond(HttpStatusCode.BadRequest, "application/json", TestData.ValidationErrorBody);

        using var provider = host.Build();
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        var ex = await act.Should().ThrowAsync<VoyageAIValidationException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.ApiDetail.Should().Be("Input is invalid: model not found");
    }

    [Fact]
    public async Task EmbedAsync_401_ThrowsAuthException()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond(HttpStatusCode.Unauthorized, "application/json",
                """{ "detail": "Invalid API key" }""");

        using var provider = host.Build();
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        var ex = await act.Should().ThrowAsync<VoyageAIAuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ex.Which.ApiDetail.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task EmbedAsync_429WithRetryAfterSeconds_ThrowsRateLimitException()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond(req =>
            {
                // 429 with 3 retries → the pipeline retries each then the final 429 surfaces.
                var msg = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{ "detail": "slow down" }""", Encoding.UTF8, "application/json"),
                };
                msg.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                return msg;
            });

        // Disable retries so the single 429 surfaces immediately with its Retry-After intact.
        using var provider = host.Build(o => o.MaxRetryAttempts = 1);
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        var ex = await act.Should().ThrowAsync<VoyageAIRateLimitException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EmbedAsync_429WithHttpDateRetryAfter_ParsesToPositiveTimeSpan()
    {
        using var host = new TestHost();
        // Use a near-future HTTP-date so the Polly retry delay is small (< 2s)
        // and doesn't exceed the HttpClient.Timeout.
        host.MockHttp.When("*/embeddings")
            .Respond(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{ "detail": "slow down" }""", Encoding.UTF8, "application/json"),
                };
                msg.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(1));
                return msg;
            });

        using var provider = host.Build(o =>
        {
            o.MaxRetryAttempts = 1;
            // Short timeout so the test doesn't hang even if retry delay runs long.
            o.RequestTimeout = TimeSpan.FromSeconds(30);
        });
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var ex = await Assert.ThrowsAsync<VoyageAIRateLimitException>(
            () => client.EmbedAsync(TestData.SingleEmbeddingRequest()));

        ex.RetryAfter.Should().NotBeNull();
        ex.RetryAfter!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        // The parsed Retry-After is date - UtcNow at parse time, which is ≤ the original
        // 1-second offset plus any time elapsed between setup and parse.
        ex.RetryAfter!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task EmbedAsync_500_ThrowsVoyageAIExceptionWithStatusCode()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond(req =>
            {
                // 500 retried 3x by default → disable retries to surface immediately.
                var msg = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(
                        """{ "detail": "boom" }""", Encoding.UTF8, "application/json"),
                };
                return msg;
            });

        using var provider = host.Build(o => o.MaxRetryAttempts = 1);
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        // 500 maps to the base VoyageAIException (not a derived type): ThrowExactly rejects
        // the derived auth/rate-limit/validation types, proving the default switch branch ran.
        var ex = await act.Should().ThrowExactlyAsync<VoyageAIException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        ex.Which.ApiDetail.Should().Be("boom");
    }

    [Fact]
    public async Task EmbedAsync_EmptySuccessBody_ThrowsJsonException()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/embeddings")
            .Respond("application/json", " ");

        using var provider = host.Build();
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(TestData.SingleEmbeddingRequest());

        // NOTE (production gap): ReadFromJsonAsync throws JsonException on empty input
        // before VoyageAIBaseClient's null-check ("empty response body") can fire. The
        // base client should catch JsonException and wrap it as VoyageAIException. Once
        // fixed, this test should assert VoyageAIException with "empty response body".
        var ex = await act.Should().ThrowAsync<JsonException>();
        ex.Which.Message.Should().Contain("JSON tokens");
    }

    [Fact]
    public async Task EmbedAsync_NullRequest_ThrowsArgumentNullException()
    {
        using var host = new TestHost();
        using var provider = host.Build();
        var client = provider.GetRequiredService<IEmbeddingsClient>();

        var act = () => client.EmbedAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
