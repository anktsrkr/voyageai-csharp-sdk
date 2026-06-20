namespace VoyageAI.Tests.Unit.MicrosoftExtensionsAI.Helpers;

/// <summary>
/// A controllable stand-in for <see cref="IEmbeddingsClient"/> used by the MEAI generator
/// tests. Unlike the HTTP-layer SDK tests (which use MockHttp against the real DI pipeline),
/// the generator only depends on the interface, so a simple recording fake is enough and
/// faster. It captures the last request for assertion and returns a caller-built response.
/// </summary>
public sealed class FakeEmbeddingsClient : IEmbeddingsClient
{
    /// <summary>The number of times <see cref="EmbedAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>The most recent request passed to <see cref="EmbedAsync"/>, for assertion.</summary>
    public EmbeddingRequest? LastRequest { get; private set; }

    /// <summary>
    /// The canned response to return from the next call. When <see langword="null"/>, the
    /// fake builds a deterministic response from the request: one zero-filled vector per
    /// input, matching model/usage. Set this to return a bespoke shape.
    /// </summary>
    public EmbeddingResponse? NextResponse { get; set; }

    /// <summary>
    /// An optional exception to throw on the next call instead of returning a response.
    /// Useful for verifying the generator does not swallow client errors.
    /// </summary>
    public Exception? NextException { get; set; }

    /// <inheritdoc/>
    public Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;

        if (NextException is not null)
        {
            return Task.FromException<EmbeddingResponse>(NextException);
        }

        if (NextResponse is not null)
        {
            return Task.FromResult(NextResponse);
        }

        // Deterministic default: one 3-dim vector per input, ordered by index, with a
        // usage of 1 token per input so tests can assert the mapping without bespoke data.
        int count = request.Input.Count;
        var data = new List<EmbeddingObject>(count);
        for (int i = 0; i < count; i++)
        {
            data.Add(TestData.Embedding(i, 0.1f * i, 0.2f * i, 0.3f * i));
        }

        return Task.FromResult(TestData.EmbeddingResponse(request.Model, count, data));
    }
}
