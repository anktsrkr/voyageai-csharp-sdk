using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Validation;

/// <summary>
/// Boundary tests for the client-side <see cref="Guard"/> checks. These run before the
/// network call, so no <see cref="Http.HttpClient"/> is involved — we exercise the guard
/// methods directly at the off-by-one edges and the custom-max override.
/// </summary>
public class GuardTests
{
    [Theory]
    [InlineData(128, true)]   // boundary in-range → no throw
    [InlineData(129, false)]  // one over → throw
    [InlineData(1, true)]
    [InlineData(0, true)]
    public void ValidateBatchSize_Boundary(int count, bool shouldSucceed)
    {
        var input = EmbeddingInput.From(Enumerable.Range(0, count).Select(i => i.ToString()).ToArray());

        var act = () => Guard.ValidateBatchSize(input);

        if (shouldSucceed)
        {
            act.Should().NotThrow();
        }
        else
        {
            var ex = act.Should().Throw<VoyageAIValidationException>().Which;
            ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            ex.Message.Should().Contain("128");
            ex.Message.Should().Contain(count.ToString());
        }
    }

    [Fact]
    public void ValidateBatchSize_CustomMax_UsesProvidedLimit()
    {
        var input = EmbeddingInput.From("a", "b", "c");

        // default max would pass, but custom max of 2 rejects a batch of 3
        var ex = Act.Assert(() => Guard.ValidateBatchSize(input, maxBatch: 2));
        ex.Message.Should().Contain("2");
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void ValidateDocuments_Boundary(int count, bool shouldSucceed)
    {
        var docs = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray();

        var act = () => Guard.ValidateDocuments(docs);

        if (shouldSucceed)
        {
            act.Should().NotThrow();
        }
        else
        {
            var ex = act.Should().Throw<VoyageAIValidationException>().Which;
            ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            ex.Message.Should().Contain("1000");
        }
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void ValidateMultimodalInputs_Boundary(int count, bool shouldSucceed)
    {
        // Each multimodal input needs at least one content part.
        var inputs = Enumerable.Range(0, count)
            .Select(_ => MultimodalInput.From(new TextContentPart("x")))
            .ToArray();

        var act = () => Guard.ValidateMultimodalInputs(inputs);

        if (shouldSucceed)
        {
            act.Should().NotThrow();
        }
        else
        {
            var ex = act.Should().Throw<VoyageAIValidationException>().Which;
            ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            ex.Message.Should().Contain("1000");
        }
    }
}

/// <summary>Small local helper to keep the parameterless-Action assertions readable.</summary>
file static class Act
{
    public static VoyageAIValidationException Assert(Action action) =>
        FluentActions.Invoking(action).Should().Throw<VoyageAIValidationException>().Which;
}
