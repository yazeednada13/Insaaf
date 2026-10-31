using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories.Projections;

public sealed record RestaurantListItemProjection(
    Guid Id,
    string Name,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? ImageUrl,
    IReadOnlyList<CategoryProjection> Categories,
    decimal? AverageRating,
    int ReviewCount,
    string? TimeZoneId,
    IReadOnlyList<OpeningHoursProjection> OpeningHours);
