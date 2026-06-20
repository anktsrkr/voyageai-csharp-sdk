namespace VoyageAI.Tests.Unit.AgentFramework.Helpers;

/// <summary>
/// Test record that implements <see cref="IVoyageSearchResultMetadata"/>, so the default
/// result mapper populates <c>SourceName</c>/<c>SourceLink</c>.
/// </summary>
public sealed record TestRecord(string Id, string Body, string SourceName, string SourceLink)
    : IVoyageSearchResultMetadata;

/// <summary>
/// Test record that does not implement <see cref="IVoyageSearchResultMetadata"/>, so the
/// default mapper emits only <c>Text</c>.
/// </summary>
public sealed record PlainRecord(string Id, string Body);
