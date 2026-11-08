namespace Insaaf.Application.Photos.Abstractions;

/// <summary>A decoded, re-encoded photo: JPEG bytes with no metadata.</summary>
public sealed record ProcessedImage(byte[] Jpeg, int Width, int Height);

/// <summary>
/// Turns an uploaded file into a safe JPEG (Phase 9A plan §7): decode, refuse anything that isn't a real
/// JPEG, PNG or WebP, apply the EXIF orientation, resize, and re-encode without any metadata.
/// </summary>
public interface IImageProcessor
{
    /// <summary>The processed image, or null when the content isn't a supported image.</summary>
    ProcessedImage? Process(ReadOnlyMemory<byte> content);
}
