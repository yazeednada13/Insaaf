using Insaaf.Domain.Enums;

namespace Insaaf.Application.Restaurants.Dtos;

public sealed record RestaurantSearchQuery(
    string? Search = null,
    string? City = null,
    decimal? MinRating = null,
    int? PriceLevel = null,
    string? Category = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CategoryDto(Guid Id, string Name, string Slug);

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
    int ReviewCount);

public sealed record RestaurantListResultDto(
    IReadOnlyList<RestaurantListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

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
    int ReviewCount);
