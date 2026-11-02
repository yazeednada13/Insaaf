using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Verification;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Admin;

public sealed record SetPartnershipRequest(bool IsPartner);

public sealed record RestaurantPartnershipDto(Guid RestaurantId, bool IsActive, bool IsPartner, DateTime? PartnerSince);

public interface IRestaurantPartnershipService
{
    Task<RestaurantPartnershipDto> SetAsync(
        Guid restaurantId,
        SetPartnershipRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks a restaurant as an Insaaf partner after an agreement. Only partners can have visit tokens;
/// ending a partnership makes all of its tokens unusable at resolve and submit immediately.
/// </summary>
public sealed class RestaurantPartnershipService : IRestaurantPartnershipService
{
    private readonly IRestaurantRepository _restaurants;
    private readonly IDateTimeProvider _clock;

    public RestaurantPartnershipService(IRestaurantRepository restaurants, IDateTimeProvider clock)
    {
        _restaurants = restaurants;
        _clock = clock;
    }

    public async Task<RestaurantPartnershipDto> SetAsync(
        Guid restaurantId,
        SetPartnershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _restaurants.SetPartnershipAsync(restaurantId, request.IsPartner, _clock.UtcNow, cancellationToken)
            ?? throw new VerificationException("not_found", "Restaurant was not found.", VerificationStatusCodes.NotFound);

        return new RestaurantPartnershipDto(result.Id, result.IsActive, result.IsPartner, result.PartnerSince);
    }
}
