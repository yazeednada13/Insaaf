using Insaaf.Domain.Enums;

namespace Insaaf.Application.Verification;

/// <summary>
/// Who is issuing visit tokens, as established by the server (never read from a request body).
/// <para>
/// <see cref="RestrictedToRestaurantId"/> limits the principal to one restaurant. Phase 5 only creates
/// unrestricted Admin principals; a future integration channel would derive the restaurant from its own
/// credential and be restricted to it, and <see cref="VisitTokenIssuer"/> enforces that scope.
/// </para>
/// </summary>
public sealed record IssuancePrincipal(
    VisitTokenIssuanceChannel Channel,
    Guid? UserId,
    Guid? RestrictedToRestaurantId)
{
    /// <summary>An Admin may issue for any eligible partner restaurant through the manual channel.</summary>
    public static IssuancePrincipal ForAdmin(Guid userId) =>
        new(VisitTokenIssuanceChannel.Manual, userId, RestrictedToRestaurantId: null);

    public bool CanActFor(Guid restaurantId) =>
        RestrictedToRestaurantId is null || RestrictedToRestaurantId == restaurantId;
}
