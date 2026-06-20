namespace VoyageAI;

/// <summary>
/// Opt-in interface for record types that carry citation metadata. When a record
/// implements this, <see cref="VoyageRagContextProvider"/> can build a
/// <c>TextSearchResult</c> with <c>SourceName</c>/<c>SourceLink</c> set without the
/// caller supplying a custom result mapper. Record types that don't fit this shape use a
/// <see cref="VoyageRagContextProviderOptions{T}.ResultMapper"/> lambda instead.
/// </summary>
public interface IVoyageSearchResultMetadata
{
    /// <summary>Human-readable source name surfaced as a citation.</summary>
    string SourceName { get; }

    /// <summary>URL surfaced as a citation link.</summary>
    string SourceLink { get; }
}
