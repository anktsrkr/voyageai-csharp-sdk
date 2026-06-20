using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Clients;

/// <summary>
/// Happy-path and rate-limit coverage for <see cref="MultimodalEmbeddingsClient"/>. The
/// shared error mapping in <see cref="VoyageAIBaseClient"/> is covered by
/// <see cref="EmbeddingsClientTests"/>; here we prove the endpoint wires through and that
/// the multimodal usage shape deserializes, plus 429 + Retry-After propagation.
/// </summary>
public class MultimodalEmbeddingsClientTests
{
    [Fact]
    public async Task EmbedAsync_HappyPath_DeserializesResponseAndSendsBearerToken()
    {
        using var host = new TestHost();
        HttpRequestMessage? captured = null;
        host.MockHttp.When("*/multimodalembeddings")
            .Respond(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        TestData.MultimodalResponseBody, Encoding.UTF8, "application/json"),
                };
            });

        using var provider = host.Build(o => o.ApiKey = "mm-key");
        var client = provider.GetRequiredService<IMultimodalEmbeddingsClient>();

        var response = await client.EmbedAsync(TestData.MultimodalRequest());

        response.Data.Should().HaveCount(1);
        response.Data[0].Embedding.Should().Equal(0.5f, 0.6f, 0.7f, 0.8f);
        response.Model.Should().Be("voyage-multimodal-3");
        response.Usage.TextTokens.Should().Be(4);
        response.Usage.ImagePixels.Should().Be(0);
        response.Usage.TotalTokens.Should().Be(4);

        captured!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization!.Parameter.Should().Be("mm-key");
    }

    [Fact]
    public async Task EmbedAsync_429WithRetryAfter_PropagatesRetryAfter()
    {
        using var host = new TestHost();
        host.MockHttp.When("*/multimodalembeddings")
            .Respond(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{ "detail": "slow down" }""", Encoding.UTF8, "application/json"),
                };
                msg.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(8));
                return msg;
            });

        using var provider = host.Build(o => o.MaxRetryAttempts = 1);
        var client = provider.GetRequiredService<IMultimodalEmbeddingsClient>();

        var ex = await Assert.ThrowsAsync<VoyageAIRateLimitException>(
            () => client.EmbedAsync(TestData.MultimodalRequest()));

        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task EmbedAsync_NullRequest_ThrowsArgumentNullException()
    {
        using var host = new TestHost();
        using var provider = host.Build();
        var client = provider.GetRequiredService<IMultimodalEmbeddingsClient>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmbedAsync(null!));
    }
}
