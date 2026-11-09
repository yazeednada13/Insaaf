using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Social;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Photos;

public interface IRestaurantPhotoService
{
    Task<RestaurantPhotoListResultDto> ListAsync(Guid restaurantId, RestaurantPhotosQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// «صور المجتمع» on the restaurant screen (Phase 11): photos from the restaurant's verified public reviews.
/// Moderation applies automatically: a hidden review, a removed photo or an inactive restaurant drops out.
/// </summary>
public sealed class RestaurantPhotoService : IRestaurantPhotoService
{
    private readonly IRestaurantRepository _restaurants;
    private readonly IReviewPhotoRepository _photos;

    public RestaurantPhotoService(IRestaurantRepository restaurants, IReviewPhotoRepository photos)
    {
        _restaurants = restaurants;
        _photos = photos;
    }

    public async Task<RestaurantPhotoListResultDto> ListAsync(
        Guid restaurantId,
        RestaurantPhotosQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > FeedCursorCodec.MaxLimit)
        {
            throw new RestaurantException("validation_error", $"Limit must be between 1 and {FeedCursorCodec.MaxLimit}.");
        }

        FeedCursor? after;
        try
        {
            after = FeedCursorCodec.Decode(query.Cursor);
        }
        catch (SocialException)
        {
            throw new RestaurantException("validation_error", "Cursor is invalid.");
        }

        if (!await _restaurants.IsActiveAsync(restaurantId, cancellationToken))
        {
            throw new RestaurantException("not_found", "Restaurant was not found.", RestaurantStatusCodes.NotFound);
        }

        var rows = await _photos.ListForRestaurantAsync(restaurantId, after, query.Limit + 1, cancellationToken);
        var items = rows.Take(query.Limit).ToList();
        return new RestaurantPhotoListResultDto(
            items.Select(photo =>
            {
                var dto = ReviewPhotoDto.From(photo.Id, photo.Width, photo.Height);
                return new RestaurantPhotoDto(photo.Id, photo.ReviewId, dto.Url, dto.ThumbnailUrl, photo.Width, photo.Height);
            }).ToList(),
            rows.Count > query.Limit ? FeedCursorCodec.Encode(items[^1].AttachedAt, items[^1].Id) : null);
    }
}
