using System.Text;
using Insaaf.Infrastructure.Photos;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Insaaf.Application.Tests.Photos;

/// <summary>Phase 9A, task 9A.1b: image processing and local storage.</summary>
public sealed class ImageProcessingTests : IDisposable
{
    private readonly SkiaImageProcessor _processor = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"insaaf-photos-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ── Processing ────────────────────────────────────────────────────────────

    [Fact]
    public void Jpeg_WithGpsExif_IsReencodedWithoutAnyMetadata()
    {
        var jpeg = WithExif(Encode(TwoColor(20, 10), SKEncodedImageFormat.Jpeg), orientation: 1);
        Assert.Contains("GPSTESTMARKER", Encoding.ASCII.GetString(jpeg));

        var result = _processor.Process(jpeg);

        Assert.NotNull(result);
        var output = Encoding.ASCII.GetString(result.Jpeg);
        Assert.DoesNotContain("GPSTESTMARKER", output);
        Assert.DoesNotContain("Exif", output);
        Assert.Equal((20, 10), (result.Width, result.Height));
        using var codec = SKCodec.Create(SKData.CreateCopy(result.Jpeg));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
    }

    [Fact]
    public void ExifOrientation_IsAppliedToThePixels()
    {
        // Orientation 6 = rotate 90° clockwise to display. The left (red) half becomes the top half.
        var jpeg = WithExif(Encode(TwoColor(20, 10), SKEncodedImageFormat.Jpeg), orientation: 6);

        var result = _processor.Process(jpeg)!;

        Assert.Equal((10, 20), (result.Width, result.Height));
        using var bitmap = SKBitmap.Decode(result.Jpeg);
        Assert.True(IsRed(bitmap.GetPixel(5, 3)));
        Assert.True(IsBlue(bitmap.GetPixel(5, 16)));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void PngAndWebp_AreAccepted_AndStoredAsJpeg(SKEncodedImageFormat format)
    {
        var result = _processor.Process(Encode(TwoColor(30, 20), format));

        Assert.NotNull(result);
        Assert.Equal(0xFF, result.Jpeg[0]);
        Assert.Equal(0xD8, result.Jpeg[1]);
    }

    [Fact]
    public void LargeImages_AreResizedToTheMaximumSide()
    {
        var result = _processor.Process(Encode(TwoColor(3000, 1000), SKEncodedImageFormat.Jpeg))!;

        Assert.Equal(2048, result.Width);
        Assert.Equal(683, result.Height);
    }

    [Fact]
    public void TextRenamedAsJpeg_GifAndEmptyContent_AreRefused()
    {
        var text = Encoding.UTF8.GetBytes("this is not an image, only text pretending to be one.jpg");
        // A valid 1×1 GIF: a real image, but not an allowed format.
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==");

        Assert.Null(_processor.Process(text));
        Assert.Null(_processor.Process(gif));
        Assert.Null(_processor.Process(ReadOnlyMemory<byte>.Empty));
    }

    // ── Local storage ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Storage_WritesEachAttemptToItsOwnFile_AndNeverOverwrites()
    {
        var storage = CreateStorage();
        var photoId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var firstKey = await storage.SaveAttemptAsync(photoId, first, new byte[] { 1, 2, 3 });
        var secondKey = await storage.SaveAttemptAsync(photoId, second, new byte[] { 9 });

        Assert.NotEqual(firstKey, secondKey);
        await using (var stream = await storage.OpenReadAsync(firstKey))
        {
            Assert.Equal(3, stream!.Length);
        }

        Assert.Equal(2, (await storage.ListFolderAsync(photoId)).Count);
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAttemptAsync(photoId, first, new byte[] { 7 }));
    }

    [Fact]
    public async Task Storage_RefusesKeysOutsideItsLayout_AndDeletesIdempotently()
    {
        var storage = CreateStorage();
        var photoId = Guid.NewGuid();
        await storage.SaveAttemptAsync(photoId, Guid.NewGuid(), new byte[] { 1 });

        Assert.Null(await storage.OpenReadAsync("../../secret.txt"));
        Assert.Null(await storage.OpenReadAsync($"{photoId:N}/../x.jpg"));

        await storage.DeleteFolderAsync(photoId);
        await storage.DeleteFolderAsync(photoId);
        Assert.Empty(await storage.ListFolderAsync(photoId));
    }

    [Fact]
    public void StorageRoot_InsideTheContentRoot_IsRefused()
    {
        var contentRoot = Path.Combine(_root, "api");

        Assert.Throws<InvalidOperationException>(() =>
            PhotoStorageOptions.EnsureOutsideContentRoot(Path.Combine(contentRoot, "photos"), contentRoot));
        PhotoStorageOptions.EnsureOutsideContentRoot(Path.Combine(_root, "photos"), contentRoot);
    }

    [Fact]
    public void DefaultStorageRoot_IsAPerUserFolderOutsideTheRepository()
    {
        var root = new PhotoStorageOptions().ResolveRoot();

        Assert.EndsWith(Path.Combine("Insaaf", "review-photos"), root);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            root,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LocalPhotoStorage CreateStorage() =>
        new(Options.Create(new PhotoStorageOptions { RootPath = _root }));

    private static SKBitmap TwoColor(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var red = new SKPaint { Color = SKColors.Red };
        using var blue = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(0, 0, width / 2f, height, red);
        canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, 95))
        {
            return data.ToArray();
        }
    }

    private static bool IsRed(SKColor color) => color.Red > 180 && color.Blue < 80;

    private static bool IsBlue(SKColor color) => color.Blue > 180 && color.Red < 80;

    // Inserts an APP1 EXIF segment after SOI with Orientation, ImageDescription and a GPS IFD.
    private static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var tiff = new List<byte>();
        void U16(int value) { tiff.Add((byte)value); tiff.Add((byte)(value >> 8)); }
        void U32(int value) { U16(value & 0xFFFF); U16(value >> 16); }
        void Entry(int tag, int type, int count, int value) { U16(tag); U16(type); U32(count); U32(value); }

        const int ifd0 = 8;
        const int descriptionOffset = ifd0 + 2 + (3 * 12) + 4;   // 50
        const int gpsIfd = descriptionOffset + 16;                // 66
        tiff.AddRange("II"u8.ToArray());
        U16(42);
        U32(ifd0);
        U16(3);
        Entry(0x010E, 2, 16, descriptionOffset);                  // ImageDescription
        Entry(0x0112, 3, 1, orientation);                         // Orientation
        Entry(0x8825, 4, 1, gpsIfd);                              // GPSInfo IFD pointer
        U32(0);
        tiff.AddRange("GPSTESTMARKER12\0"u8.ToArray());
        U16(1);
        Entry(0x0001, 2, 2, 'N');                                 // GPSLatitudeRef = "N"
        U32(0);

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
    }
}
