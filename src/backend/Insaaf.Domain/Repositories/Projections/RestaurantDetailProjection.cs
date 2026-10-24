using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories.Projections;

public sealed record RestaurantDetailProjection(
    Guid Id,
    string Name,
    string Description,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? Phone,
    string? ImageUrl,
    IReadOnlyList<CategoryProjection> Categories,
    decimal? AverageRating,
    int ReviewCount);
