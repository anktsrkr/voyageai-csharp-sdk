using System.Text.Json.Serialization;
using VoyageAI.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// Represents the <c>input</c> field of an embeddings request, which the Voyage AI
/// API accepts as either a single text string or a list of text strings (max 128).
/// </summary>
/// <remarks>
/// Construct with <see cref="From(string)"/> / <see cref="From(IEnumerable{string})"/>,
/// the implicit conversions from <see cref="string"/> / <see cref="string"/>[], or
/// the <c>EmbeddingInput.Single</c> / <c>EmbeddingInput.Batch</c> factory methods.
/// Serialization as a bare string or a JSON array is handled by
/// <see cref="EmbeddingInputConverter"/>; this type carries no JSON attributes so the
/// source generator emits it as an opaque value.
/// </remarks>
[JsonConverter(typeof(EmbeddingInputConverter))]
public sealed class EmbeddingInput
{
    /// <summary>Initializes an empty instance. Prefer the factory methods.</summary>
    public EmbeddingInput() { }

    /// <summary>Initializes a single-string input.</summary>
    internal EmbeddingInput(string single)
    {
        Single = single;
        IsBatch = false;
    }

    /// <summary>Initializes a batch input.</summary>
    internal EmbeddingInput(IReadOnlyList<string> batch)
    {
        Batch = batch;
        IsBatch = true;
    }

    /// <summary>The single string value, when this input is not a batch.</summary>
    public string? Single { get; }

    /// <summary>The list of strings, when this input is a batch.</summary>
    public IReadOnlyList<string>? Batch { get; }

    /// <summary><see langword="true"/> when this instance wraps a list of strings.</summary>
    [JsonIgnore]
    public bool IsBatch { get; }

    /// <summary>Total number of inputs represented (1 for a single string, otherwise the batch length).</summary>
    [JsonIgnore]
    public int Count => IsBatch ? Batch!.Count : 1;

    /// <summary>Creates a single-string input.</summary>
    public static EmbeddingInput From(string text) => new(text);

    /// <summary>Creates a batch input from a list of strings.</summary>
    public static EmbeddingInput From(IEnumerable<string> texts) =>
        new(texts as IReadOnlyList<string> ?? texts.ToList());

    /// <summary>Creates a batch input from a list of strings.</summary>
    public static EmbeddingInput From(params string[] texts) => new(texts);

    /// <summary>Implicitly converts a single string to an input.</summary>
    public static implicit operator EmbeddingInput(string text) => new(text);

    /// <summary>Implicitly converts a string array to a batch input.</summary>
    public static implicit operator EmbeddingInput(string[] texts) => new(texts);

    /// <summary>Implicitly converts a list of strings to a batch input.</summary>
    public static implicit operator EmbeddingInput(List<string> texts) => new(texts);

    /// <inheritdoc/>
    public override string ToString() =>
        IsBatch ? $"EmbeddingInput[Batch:{Batch!.Count}]" : $"EmbeddingInput[Single]";
}
