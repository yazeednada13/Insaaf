namespace Insaaf.Application.Photos.Abstractions;

/// <summary>
/// A decoded, re-encoded photo: JPEG bytes with no metadata, plus a smaller copy for cards
/// (Phase 10, D-7; the same bytes when the photo is already small).
/// </summary>
public sealed record ProcessedImage(byte[] Jpeg, int Width, int Height, byte[] ThumbnailJpeg);

/// <summary>
/// Turns an uploaded file into a safe JPEG (Phase 9A plan §7): decode, refuse anything that isn't a real
/// JPEG, PNG or WebP, apply the EXIF orientation, resize, and re-encode without any metadata.
/// </summary>
public interface IImageProcessor
{
    /// <summary>The processed image, or null when the content isn't a supported image.</summary>
    ProcessedImage? Process(ReadOnlyMemory<byte> content);

    /// <summary>
    /// A profile photo (Phase 11): the same checks and metadata removal, then a centred square crop of at
    /// most 512 px. Null when the content isn't a supported image.
    /// </summary>
    ProcessedImage? ProcessAvatar(ReadOnlyMemory<byte> content);
}
