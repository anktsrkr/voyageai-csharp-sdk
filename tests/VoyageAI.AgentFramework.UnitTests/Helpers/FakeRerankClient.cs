namespace VoyageAI.Tests.Unit.AgentFramework.Helpers;

/// <summary>
/// A controllable stand-in for <see cref="IRerankClient"/>. Records the last request so
/// tests can assert how <see cref="VoyageReranker"/> maps options onto a
/// <see cref="RerankRequest"/>, and returns a caller-built response.
/// </summary>
public sealed class FakeRerankClient : IRerankClient
{
    /// <summary>The number of times <see cref="RerankAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>The most recent request passed to <see cref="RerankAsync"/>, for assertion.</summary>
    public RerankRequest? LastRequest { get; private set; }

    /// <summary>
    /// The results returned from the next call, in order. When <see langword="null"/> the
    /// fake returns a deterministic identity ordering (each document at its own index).
    /// </summary>
    public IReadOnlyList<RerankResult>? NextResults { get; set; }

    /// <inheritdoc/>
    public Task<RerankResponse> RerankAsync(
        RerankRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;

        IReadOnlyList<RerankResult> data = NextResults ?? IdentityOrdering(request.Documents.Count);
        return Task.FromResult(new RerankResponse
        {
            Object = "list",
            Data = data,
            Model = request.Model,
            Usage = new UsageInfo()
        });
    }

    /// <summary>Builds a rerank result per document, each pointing at its own index.</summary>
    private static IReadOnlyList<RerankResult> IdentityOrdering(int count)
    {
        var results = new RerankResult[count];
        for (int i = 0; i < count; i++)
        {
            results[i] = new RerankResult { Index = i, RelevanceScore = 1.0f - (0.1f * i) };
        }

        return results;
    }
}
