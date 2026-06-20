using System.Text.Json.Serialization;

namespace VoyageAI.Models;

/// <summary>
/// A single piece of multimodal content: text, an image URL, or a Base64 image.
/// Used inside <see cref="MultimodalInput"/>.
/// </summary>
/// <remarks>
/// The discriminator is the <c>type</c> property (<c>text</c>, <c>image_url</c>, or
/// <c>image_base64</c>). Serialization is polymorphic via STJ attributes — AOT/trim safe.
/// Construct concrete parts directly (<c>new TextContentPart("...")</c>) or via the
/// <c>Create</c> factory methods on <see cref="MultimodalInput"/>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentPart), "text")]
[JsonDerivedType(typeof(ImageUrlContentPart), "image_url")]
[JsonDerivedType(typeof(ImageBase64ContentPart), "image_base64")]
public abstract record ContentPart;

/// <summary>A text content part. Serializes with <c>type: "text"</c>.</summary>
public sealed record TextContentPart : ContentPart
{
    /// <summary>Initializes a new <see cref="TextContentPart"/>.</summary>
    public TextContentPart(string text) => Text = text;

    /// <summary>The text content.</summary>
    public string Text { get; init; }
}

/// <summary>An image referenced by URL. Serializes with <c>type: "image_url"</c>.</summary>
public sealed record ImageUrlContentPart : ContentPart
{
    /// <summary>Initializes a new <see cref="ImageUrlContentPart"/>.</summary>
    public ImageUrlContentPart(string imageUrl) => ImageUrl = imageUrl;

    /// <summary>The image URL.</summary>
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; init; }
}

/// <summary>A Base64-encoded image in data-URL format. Serializes with <c>type: "image_base64"</c>.</summary>
public sealed record ImageBase64ContentPart : ContentPart
{
    /// <summary>Initializes a new <see cref="ImageBase64ContentPart"/>.</summary>
    public ImageBase64ContentPart(string imageBase64) => ImageBase64 = imageBase64;

    /// <summary>Base64-encoded image in <c>data:[mediatype];base64,&lt;data&gt;</c> form.</summary>
    [JsonPropertyName("image_base64")]
    public string ImageBase64 { get; init; }
}
