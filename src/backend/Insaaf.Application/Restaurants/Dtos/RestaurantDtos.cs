using Insaaf.Domain.Enums;

namespace Insaaf.Application.Restaurants.Dtos;

public sealed record RestaurantSearchQuery(
    string? Search = null,
    string? City = null,
    decimal? MinRating = null,
    int? PriceLevel = null,
    string? Category = null,
    bool? OpenNow = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CategoryDto(Guid Id, string Name, string Slug);

/// <remarks><c>IsOpen</c> is null when the restaurant has no opening-hours data.</remarks>
public sealed record RestaurantListItemDto(
    Guid Id,
    string Name,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? ImageUrl,
    IReadOnlyList<CategoryDto> Categories,
    decimal? AverageRating,
    int ReviewCount,
    bool? IsOpen);

public sealed record RestaurantListResultDto(
    IReadOnlyList<RestaurantListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Averages of the detailed ratings over public reviews, rounded to one decimal.</summary>
public sealed record RatingBreakdownDto(decimal Food, decimal Service, decimal Value, decimal Atmosphere);

/// <remarks>
/// <c>IsOpen</c> is null when the restaurant has no opening-hours data; <c>TodayHours</c> is
/// today's local periods (e.g. "10:00 ص — 1:00 ص"), or null when it has none today.
/// <c>AverageRating</c> and <c>RatingBreakdown</c> are null when there are no public reviews.
/// </remarks>
public sealed record RestaurantDetailDto(
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
    IReadOnlyList<CategoryDto> Categories,
    decimal? AverageRating,
    int ReviewCount,
    bool? IsOpen,
    string? TodayHours,
    RatingBreakdownDto? RatingBreakdown);
