using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

/// <summary>Saved restaurants and reviews (Phase 11). Every write is repeat-safe and race-safe.</summary>
public interface ISavedRepository
{
    Task AddRestaurantIfMissingAsync(SavedRestaurant saved, CancellationToken cancellationToken = default);

    Task RemoveRestaurantIfExistsAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default);

    Task<bool> IsRestaurantSavedAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default);

    /// <summary>The user's saved active restaurants, most recently saved first.</summary>
    Task<PagedResult<Guid>> ListRestaurantIdsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddReviewIfMissingAsync(SavedReview saved, CancellationToken cancellationToken = default);

    Task RemoveReviewIfExistsAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default);
}
