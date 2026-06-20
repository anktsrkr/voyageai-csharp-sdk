namespace VoyageAI.Tests.Unit.AgentFramework.Helpers;

/// <summary>
/// A controllable stand-in for <see cref="IVoyageRagSearcher{T}"/>. Records the query and
/// returns a caller-supplied candidate pool, so composition tests can assert how the
/// provider consumes the search stage without any vector store.
/// </summary>
public sealed class FakeVoyageRagSearcher<T> : IVoyageRagSearcher<T>
{
    /// <summary>The candidate pool returned from the next <see cref="SearchAsync"/> call.</summary>
    public IReadOnlyList<VoyageSearchResult<T>> NextResults { get; set; } = Array.Empty<VoyageSearchResult<T>>();

    /// <summary>The number of times <see cref="SearchAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>The most recent query passed to <see cref="SearchAsync"/>, for assertion.</summary>
    public string? LastQuery { get; private set; }

    /// <summary>An optional exception to throw on the next call instead of returning results.</summary>
    public Exception? NextException { get; set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<VoyageSearchResult<T>>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastQuery = query;

        if (NextException is not null)
        {
            return Task.FromException<IReadOnlyList<VoyageSearchResult<T>>>(NextException);
        }

        return Task.FromResult(NextResults);
    }
}
