namespace VoyageAI.Tests.Unit.AgentFramework;

/// <summary>
/// Defaults verification for <see cref="VoyageRagContextProviderOptions{T}"/> and the
/// per-call <c>configure</c> callback mutation surface.
/// </summary>
public class VoyageRagContextProviderOptionsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new VoyageRagContextProviderOptions<TestRecord>();

        // RerankerOptions is non-nullable so consumers never need to coalesce with new().
        options.RerankerOptions.Should().NotBeNull();
        options.RerankerOptions.Model.Should().Be(VoyageAIModels.Rerank2);
        options.RerankerOptions.TopK.Should().Be(5);
        options.RerankerOptions.Truncation.Should().BeTrue();
        options.ResultMapper.Should().BeNull();
        options.TextSearchOptions.Should().BeNull();
    }

    [Fact]
    public void Options_AreMutable()
    {
        var options = new VoyageRagContextProviderOptions<TestRecord>();

        options.RerankerOptions.TopK = 7;
        options.TextSearchOptions = new TextSearchProviderOptions();
        // The mapper is typed against the record, so it reads fields directly — no cast.
        options.ResultMapper = c => new TextSearchProvider.TextSearchResult { Text = c.Text };

        options.RerankerOptions.TopK.Should().Be(7);
        options.TextSearchOptions.Should().NotBeNull();
        options.ResultMapper.Should().NotBeNull();
    }
}
