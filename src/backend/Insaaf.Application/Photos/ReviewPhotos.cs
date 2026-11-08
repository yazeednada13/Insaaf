using Insaaf.Application.Photos.Dtos;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Photos;

/// <summary>Maps a review's Ready photos to their public form, with a relative image URL.</summary>
public static class ReviewPhotos
{
    public static IReadOnlyList<ReviewPhotoDto> ToDtos(IReadOnlyList<ReviewPhotoProjection>? photos) =>
        photos is null or { Count: 0 }
            ? []
            : photos.Select(photo => ReviewPhotoDto.From(photo.Id, photo.Width, photo.Height)).ToList();
}
