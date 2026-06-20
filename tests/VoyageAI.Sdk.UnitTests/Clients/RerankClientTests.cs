using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Clients;

/// <summary>
/// Happy-path and rate-limit coverage for <see cref="RerankClient"/>. The shared error
/// mapping in <see cref="VoyageAIBaseClient"/> is covered by
/// <see cref="EmbeddingsClientTests"/>; here we prove the rerank endpoint wires through,
/// the <c>relevance_score</c>/<c>index</c>/<c>document</c> fields map, and 429 +
/// Retry-After propagates.
/// </summary>
public class RerankClientTests
{
    [Fact]
    public async Task RerankAsync_HappyPath_DeserializesResponseAndSendsBearerToken()
    {
        using var host = new TestHost();
        HttpRequestMessage? captured = null;
        host.MockHttp.When("*/rerank")
            .Respond(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        TestData.RerankResponseBody, Encoding.UTF8, "application/json"),
                };
            });

        using var provider = host.Build(o => o.ApiKey = "rerank-key");
        var client = provider.GetRequiredService<IRerankClient>();

        var response = await client.RerankAsync(TestData.RerankRequest());

        response.Data.Should().HaveCount(2);
        // Sorted by descending relevance per the API contract.
        response.Data[0].Index.Should().Be(1);
        response.Data[0].RelevanceScore.Should().BeApproximately(0.95f, 0.0001f);
        response.Data[0].Document.Should().Be("second doc");
        response.Data[1].Index.Should().Be(0);
        response.Model.Should().Be("rerank-2");
        response.Usage.TotalTokens.Should().Be(20);

        captured!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization!.Parameter.Should().Be("rerank-key");
    }

    [Fact]
    public async Task RerankAsync_429WithRetryAfter_PropagatesRetryAfter()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/rerank")
            .Respond(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{ "detail": "slow down" }""", Encoding.UTF8, "application/json"),
                };
                msg.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(11));
                return msg;
            });

        using var provider = host.Build(o => o.MaxRetryAttempts = 1);
        var client = provider.GetRequiredService<IRerankClient>();

        var ex = await Assert.ThrowsAsync<VoyageAIRateLimitException>(
            () => client.RerankAsync(TestData.RerankRequest()));

        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(11));
    }

    [Fact]
    public async Task RerankAsync_NullRequest_ThrowsArgumentNullException()
    {
        using var host = new TestHost();
        using var provider = host.Build();
        var client = provider.GetRequiredService<IRerankClient>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.RerankAsync(null!));
    }
}
