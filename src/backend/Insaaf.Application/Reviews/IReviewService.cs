using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Reviews;

/// <summary>Read-only review use cases. Creation happens through verification only (Phase 5).</summary>
public interface IReviewService
{
    Task<ReviewListResultDto> ListForRestaurantAsync(
        Guid restaurantId,
        RestaurantReviewsQuery query,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>One public review with sub-ratings and social counts for the viewer.</summary>
    Task<ReviewDetailDto> GetByIdAsync(
        Guid reviewId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);
}
