using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface ISavedService
{
    Task<SavedStateDto> SaveRestaurantAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default);

    Task<SavedStateDto> UnsaveRestaurantAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default);

    Task<SavedStateDto> SaveReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default);

    Task<SavedStateDto> UnsaveReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default);

    Task<bool> IsRestaurantSavedAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default);

    Task<SavedRestaurantsResultDto> ListRestaurantsAsync(Guid userId, SavedRestaurantsQuery query, CancellationToken cancellationToken = default);

    Task<FeedResultDto> ListReviewsAsync(Guid userId, FeedQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Saved restaurants and reviews (Phase 11, Figma «المحفوظات»). Private: only the owner's id, from the
/// JWT, ever reaches these methods. Saving needs a visible target; unsaving always works, so a user can
/// clear an item that was hidden or deactivated after they saved it.
/// </summary>
public sealed class SavedService : ISavedService
{
    public const int MaxPageSize = 50;

    private readonly ISavedRepository _saved;
    private readonly IRestaurantRepository _restaurants;
    private readonly IReviewRepository _reviews;
    private readonly IDateTimeProvider _clock;

    public SavedService(
        ISavedRepository saved,
        IRestaurantRepository restaurants,
        IReviewRepository reviews,
        IDateTimeProvider clock)
    {
        _saved = saved;
        _restaurants = restaurants;
        _reviews = reviews;
        _clock = clock;
    }

    public async Task<SavedStateDto> SaveRestaurantAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        if (!await _restaurants.IsActiveAsync(restaurantId, cancellationToken))
        {
            throw new SocialException("not_found", "Restaurant was not found.", SocialStatusCodes.NotFound);
        }

        await _saved.AddRestaurantIfMissingAsync(
            new SavedRestaurant { UserId = userId, RestaurantId = restaurantId, CreatedAt = _clock.UtcNow },
            cancellationToken);
        return new SavedStateDto(restaurantId, true);
    }

    public async Task<SavedStateDto> UnsaveRestaurantAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        await _saved.RemoveRestaurantIfExistsAsync(userId, restaurantId, cancellationToken);
        return new SavedStateDto(restaurantId, false);
    }

    public async Task<SavedStateDto> SaveReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            throw SocialException.ReviewNotFound();
        }

        await _saved.AddReviewIfMissingAsync(
            new SavedReview { UserId = userId, ReviewId = reviewId, CreatedAt = _clock.UtcNow },
            cancellationToken);
        return new SavedStateDto(reviewId, true);
    }

    public async Task<SavedStateDto> UnsaveReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        await _saved.RemoveReviewIfExistsAsync(userId, reviewId, cancellationToken);
        return new SavedStateDto(reviewId, false);
    }

    public Task<bool> IsRestaurantSavedAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default) =>
        _saved.IsRestaurantSavedAsync(userId, restaurantId, cancellationToken);

    public async Task<SavedRestaurantsResultDto> ListRestaurantsAsync(
        Guid userId,
        SavedRestaurantsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            throw new SocialException("validation_error", $"Page must be at least 1 and pageSize between 1 and {MaxPageSize}.");
        }

        var page = await _saved.ListRestaurantIdsAsync(userId, query.Page, query.PageSize, cancellationToken);
        var items = await _restaurants.ListByIdsAsync(page.Items, cancellationToken);
        var byId = items.ToDictionary(item => item.Id);
        var utcNow = _clock.UtcNow;

        // The saved order decides; the item query only fills in the details.
        return new SavedRestaurantsResultDto(
            page.Items.Where(byId.ContainsKey).Select(id => RestaurantService.ToListItemDto(byId[id], utcNow)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<FeedResultDto> ListReviewsAsync(Guid userId, FeedQuery query, CancellationToken cancellationToken = default)
    {
        FeedCursorCodec.ValidateLimit(query.Limit);
        var cursor = FeedCursorCodec.Decode(query.Cursor);
        var rows = await _reviews.ListSavedAsync(userId, cursor, query.Limit + 1, cancellationToken);
        return FeedService.ToResult(rows, query.Limit);
    }
}
