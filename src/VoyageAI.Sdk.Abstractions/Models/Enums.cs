using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// Indicates whether embedding inputs are intended as a search <c>query</c> or a
/// corpus <c>document</c>. Voyage prepends a retrieval prompt accordingly. When
/// omitted (<see langword="null"/>), inputs are embedded verbatim.
/// </summary>
[JsonConverter(typeof(InputTypeJsonConverter))]
public enum InputType
{
    /// <summary>Input is a retrieval query.</summary>
    Query,

    /// <summary>Input is a corpus document to be searched against.</summary>
    Document,
}

/// <summary>
/// Numeric data type of returned embedding vectors. <see cref="Float"/> is
/// universally supported; the integer/binary variants are supported by
/// <c>voyage-3-large</c> and <c>voyage-code-3</c>.
/// </summary>
[JsonConverter(typeof(OutputDtypeJsonConverter))]
public enum OutputDtype
{
    /// <summary>32-bit single-precision floats (default, highest precision).</summary>
    Float,

    /// <summary>Signed 8-bit integers in the range [-128, 127].</summary>
    Int8,

    /// <summary>Unsigned 8-bit integers in the range [0, 255].</summary>
    UInt8,

    /// <summary>Bit-packed signed 8-bit integers (offset binary).</summary>
    Binary,

    /// <summary>Bit-packed unsigned 8-bit integers (offset binary).</summary>
    UBinary,
}

/// <summary>
/// Wire encoding of embedding vectors. <see cref="Base64"/> returns a Base64-encoded
/// NumPy array; the default (omitted) returns a native JSON array.
/// </summary>
[JsonConverter(typeof(EncodingFormatJsonConverter))]
public enum EncodingFormat
{
    /// <summary>Embeddings are Base64-encoded NumPy arrays.</summary>
    Base64,
}

public sealed class InputTypeJsonConverter : JsonConverter<InputType>
{
    public override InputType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "query" => InputType.Query,
            "document" => InputType.Document,
            _ => throw new JsonException($"Unknown InputType value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, InputType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            InputType.Query => "query",
            InputType.Document => "document",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        });
    }
}

public sealed class OutputDtypeJsonConverter : JsonConverter<OutputDtype>
{
    public override OutputDtype Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "float" => OutputDtype.Float,
            "int8" => OutputDtype.Int8,
            "uint8" => OutputDtype.UInt8,
            "binary" => OutputDtype.Binary,
            "ubinary" => OutputDtype.UBinary,
            _ => throw new JsonException($"Unknown OutputDtype value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, OutputDtype value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            OutputDtype.Float => "float",
            OutputDtype.Int8 => "int8",
            OutputDtype.UInt8 => "uint8",
            OutputDtype.Binary => "binary",
            OutputDtype.UBinary => "ubinary",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        });
    }
}

public sealed class EncodingFormatJsonConverter : JsonConverter<EncodingFormat>
{
    public override EncodingFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "base64" => EncodingFormat.Base64,
            _ => throw new JsonException($"Unknown EncodingFormat value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, EncodingFormat value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            EncodingFormat.Base64 => "base64",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        });
    }
}
