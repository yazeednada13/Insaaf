using Insaaf.Application.Photos.Dtos;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Photos;

/// <summary>Maps a review's Ready photos to their public form, with a relative image URL.</summary>
public static class ReviewPhotos
{
    private const string Extension = ".jpg";
    private const string ThumbnailExtension = ".thumb.jpg";

    /// <summary>The thumbnail written next to a photo file: <c>{photoId}/{leaseId}.thumb.jpg</c> (Phase 10, D-7).</summary>
    public static string ThumbnailKey(string key) =>
        key.EndsWith(Extension, StringComparison.Ordinal) ? key[..^Extension.Length] + ThumbnailExtension : key + ThumbnailExtension;

    public static IReadOnlyList<ReviewPhotoDto> ToDtos(IReadOnlyList<ReviewPhotoProjection>? photos) =>
        photos is null or { Count: 0 }
            ? []
            : photos.Select(photo => ReviewPhotoDto.From(photo.Id, photo.Width, photo.Height)).ToList();
}
