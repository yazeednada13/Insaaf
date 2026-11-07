using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

/// <summary>Read-only review queries. Reviews are created only through verification (Phase 5).</summary>
public interface IReviewRepository
{
    /// <summary>Public reviews of one restaurant, sorted and paged, with reviewer details.</summary>
    Task<PagedResult<ReviewListItemProjection>> ListByRestaurantAsync(
        ReviewSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>One public review with details and social counts; null when it isn't public.</summary>
    Task<ReviewDetailProjection?> GetPublicDetailAsync(
        Guid reviewId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Public reviews of active restaurants by users the viewer follows, excluding the viewer's own,
    /// newest first. Returns up to <paramref name="take"/> rows after <paramref name="after"/>.
    /// </summary>
    Task<IReadOnlyList<FeedItemProjection>> ListFollowingFeedAsync(
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>One user's public reviews of active restaurants, newest first.</summary>
    Task<IReadOnlyList<FeedItemProjection>> ListByUserAsync(
        Guid userId,
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>The review's author, or null when the review doesn't exist.</summary>
    Task<Guid?> GetAuthorIdAsync(Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>True when the review is public and its restaurant is active.</summary>
    Task<bool> IsPublicAsync(Guid reviewId, CancellationToken cancellationToken = default);

    // Admin (Phase 9). Hidden reviews and inactive restaurants are included.

    /// <summary>Any review, hidden or not, newest first; up to <paramref name="take"/> rows after <paramref name="after"/>.</summary>
    Task<IReadOnlyList<AdminReviewProjection>> ListForAdminAsync(
        Guid? restaurantId,
        Guid? userId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    Task<AdminReviewProjection?> GetForAdminAsync(Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides or unhides a review. Null when it doesn't exist; otherwise whether it changed. Unhiding clears
    /// the Admin and the reason.
    /// </summary>
    Task<bool?> SetHiddenAsync(
        Guid reviewId,
        bool hidden,
        Guid adminId,
        string? reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
