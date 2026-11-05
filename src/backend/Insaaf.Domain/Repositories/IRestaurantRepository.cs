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

    /// <summary>Partnership state of any restaurant (active or not); null when it does not exist.</summary>
    Task<RestaurantPartnershipProjection?> GetPartnershipAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sets the partnership flag; returns the new state, or null when the restaurant does not exist.</summary>
    Task<RestaurantPartnershipProjection?> SetPartnershipAsync(
        Guid id,
        bool isPartner,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct time zones of active restaurants, used to evaluate the openNow filter.</summary>
    Task<IReadOnlyList<string>> GetTimeZoneIdsAsync(CancellationToken cancellationToken = default);
}
