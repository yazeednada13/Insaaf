using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

public interface IRestaurantRepository
{
    Task<PagedResult<RestaurantListItemProjection>> SearchAsync(
        RestaurantSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<RestaurantDetailProjection?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>True when the restaurant exists and is active (publicly visible).</summary>
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Distinct time zones of active restaurants, used to evaluate the openNow filter.</summary>
    Task<IReadOnlyList<string>> GetTimeZoneIdsAsync(CancellationToken cancellationToken = default);
}
