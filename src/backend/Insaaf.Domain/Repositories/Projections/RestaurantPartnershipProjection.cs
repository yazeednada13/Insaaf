namespace Insaaf.Domain.Repositories.Projections;

public sealed record RestaurantPartnershipProjection(
    Guid Id,
    bool IsActive,
    bool IsPartner,
    DateTime? PartnerSince);
