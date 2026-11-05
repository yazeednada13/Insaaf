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

    /// <summary>True when the review is public and its restaurant is active.</summary>
    Task<bool> IsPublicAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
