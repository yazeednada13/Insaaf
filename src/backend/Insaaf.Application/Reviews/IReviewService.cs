using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Reviews;

/// <summary>Read-only review use cases (Phase 4). Creation is Phase 5, through verification only.</summary>
public interface IReviewService
{
    Task<ReviewListResultDto> ListForRestaurantAsync(
        Guid restaurantId,
        RestaurantReviewsQuery query,
        CancellationToken cancellationToken = default);
}
