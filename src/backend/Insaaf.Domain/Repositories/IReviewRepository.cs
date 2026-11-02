using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

/// <summary>Read-only review queries (Phase 4). Review creation arrives with verification in Phase 5.</summary>
public interface IReviewRepository
{
    /// <summary>Public reviews of one restaurant, sorted and paged, with reviewer details.</summary>
    Task<PagedResult<ReviewListItemProjection>> ListByRestaurantAsync(
        ReviewSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
