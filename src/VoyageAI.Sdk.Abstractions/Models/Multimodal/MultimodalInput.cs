namespace VoyageAI.Models;

/// <summary>
/// One multimodal input: an ordered sequence of text/image content parts.
/// </summary>
public sealed record MultimodalInput
{
    /// <summary>The ordered content parts (text and images) for this input.</summary>
    public required IReadOnlyList<ContentPart> Content { get; init; }

    /// <summary>Creates a multimodal input from a content sequence.</summary>
    public static MultimodalInput From(params ContentPart[] content) => new() { Content = content };
}
