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
}
