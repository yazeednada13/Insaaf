using Insaaf.Application.Photos.Abstractions;
using SkiaSharp;

namespace Insaaf.Infrastructure.Photos;

/// <summary>
/// SkiaSharp implementation of <see cref="IImageProcessor"/> (D-2). Only decoded pixels are kept: the
/// output is a fresh JPEG with no EXIF, GPS or other metadata. The EXIF orientation is applied to the
/// pixels first, transparency is flattened onto white, and the longest side is limited to 2048 px.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int MaxOutputSide = 2048;
    public const int JpegQuality = 85;

    // Refuse decompression bombs before allocating pixels.
    private const int MaxInputSide = 12_000;
    private const long MaxInputPixels = 50_000_000;

    public ProcessedImage? Process(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            return null;
        }

        using var data = SKData.CreateCopy(content.Span);
        using var codec = SKCodec.Create(data);
        if (codec is null
            || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            return null;
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0 || width > MaxInputSide || height > MaxInputSide || (long)width * height > MaxInputPixels)
        {
            return null;
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var source = new SKBitmap(info);
        var result = codec.GetPixels(info, source.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return null;
        }

        var origin = codec.EncodedOrigin;
        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var orientedWidth = swapsSides ? height : width;
        var orientedHeight = swapsSides ? width : height;

        var scale = Math.Min(1f, (float)MaxOutputSide / Math.Max(orientedWidth, orientedHeight));
        var outputWidth = Math.Max(1, (int)Math.Round(orientedWidth * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(orientedHeight * scale));

        using var surface = SKSurface.Create(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(SKMatrix.CreateScale(scale, scale).PreConcat(Orientation(origin, width, height)));
        using (var image = SKImage.FromBitmap(source))
        {
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        canvas.Flush();
        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded is null ? null : new ProcessedImage(encoded.ToArray(), outputWidth, outputHeight);
    }

    // Maps source pixels (w × h) to the upright image:
    // x' = ScaleX·x + SkewX·y + TransX; y' = SkewY·x + ScaleY·y + TransY.
    private static SKMatrix Orientation(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}
