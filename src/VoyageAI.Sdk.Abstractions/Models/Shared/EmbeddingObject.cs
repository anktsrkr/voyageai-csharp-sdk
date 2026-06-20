using System;
using System.Text.Json.Serialization;
using System.Threading;
using VoyageAI.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// A single embedding vector plus its position in the request input list.
/// </summary>
/// <remarks>
/// <para>
/// The Voyage API returns the vector in the <c>embedding</c> field as a JSON array of
/// numbers for native encoding, or — when <c>encoding_format</c> is <c>base64</c> — as a
/// single Base64-encoded NumPy array string. This record normalizes both shapes via
/// <see cref="EmbeddingObjectConverter"/>: a numeric array populates
/// <see cref="Embedding"/>; a Base64 string populates <see cref="EmbeddingBase64"/>.
/// Exactly one of the two is set per response object.
/// </para>
/// <para>
/// For non-float <c>output_dtype</c> values (<c>int8</c>, <c>uint8</c>, <c>binary</c>,
/// <c>ubinary</c>) the numbers round-trip into <see cref="Embedding"/> as <c>float</c>;
/// decode <see cref="EmbeddingBase64"/> directly when you need the exact integer
/// representation.
/// </para>
/// </remarks>
[JsonConverter(typeof(EmbeddingObjectConverter))]
public sealed record EmbeddingObject
{
    /// <summary>The object type, always <c>"embedding"</c>.</summary>
    public required string Object { get; init; } = "embedding";

    /// <summary>
    /// The embedding vector as floats. Populated for native JSON encoding (numbers);
    /// empty when the response uses Base64 encoding (see <see cref="EmbeddingBase64"/>).
    /// </summary>
    public required IReadOnlyList<float> Embedding { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Base64-encoded NumPy array, present only when <c>encoding_format</c> is
    /// <c>base64</c>; otherwise <see langword="null"/>. Mutually exclusive with a
    /// populated <see cref="Embedding"/>.
    /// </summary>
    public string? EmbeddingBase64 { get; init; }

    /// <summary>Zero-based index of this embedding within the request's input list.</summary>
    public required int Index { get; init; }

    private float[]? _cachedArray;

    /// <summary>
    /// Returns the embedding vector as a <see cref="ReadOnlyMemory{T}"/> of floats.
    /// If the vector is Base64 encoded, this decodes it as float32 elements on the first call and caches the result.
    /// </summary>
    /// <returns>A read-only view of the embedding vector.</returns>
    public ReadOnlyMemory<float> AsMemory()
    {
        var array = _cachedArray;
        if (array != null)
        {
            return array;
        }

        if (!string.IsNullOrEmpty(EmbeddingBase64))
        {
            var decoded = DecodeBase64<float>();
            Interlocked.CompareExchange(ref _cachedArray, decoded, null);
            return _cachedArray;
        }

        if (Embedding is float[] directArray)
        {
            _cachedArray = directArray;
            return directArray;
        }

        var target = System.Linq.Enumerable.ToArray(Embedding);
        Interlocked.CompareExchange(ref _cachedArray, target, null);
        return _cachedArray;
    }

    /// <summary>
    /// Decodes the base64-encoded embedding vector into a typed array.
    /// Useful for decoding quantized dtypes (such as <see cref="sbyte"/> for <c>int8</c> or <see cref="byte"/> for <c>uint8</c>).
    /// </summary>
    /// <typeparam name="T">The numerical element type (e.g. <c>float</c>, <c>sbyte</c>, or <c>byte</c>).</typeparam>
    /// <returns>A new array of <typeparamref name="T"/> containing the decoded elements.</returns>
    public T[] DecodeBase64<T>() where T : struct
    {
        if (string.IsNullOrEmpty(EmbeddingBase64))
        {
            return Array.Empty<T>();
        }

        byte[] bytes = Convert.FromBase64String(EmbeddingBase64);
        int elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        T[] array = new T[bytes.Length / elementSize];
        Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
        return array;
    }
}
