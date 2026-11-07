using System.Text.Json.Serialization;

using Insaaf.Application.Restaurants.Dtos;

namespace Insaaf.Application.Admin.Dtos;

/// <summary>
/// Create or full-update body. Every property is nullable so a missing required value is reported
/// instead of silently becoming a default. <see cref="CategorySlugs"/> and <see cref="OpeningHours"/>
/// are required; an explicit empty <see cref="OpeningHours"/> means hours are unknown.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpsertRestaurantRequest(
    string? Name,
    string? Description,
    string? City,
    string? Address,
    double? Latitude,
    double? Longitude,
    int? PriceLevel,
    string? Phone,
    string? ImageUrl,
    string? TimeZoneId,
    IReadOnlyList<string>? CategorySlugs,
    IReadOnlyList<OpeningHoursInput>? OpeningHours);

/// <summary>One period in local time. <c>dayOfWeek</c>: 0 = Sunday … 6 = Saturday; times are "HH:mm".</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpeningHoursInput(int? DayOfWeek, string? OpensAt, string? ClosesAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetRestaurantStatusRequest(bool? IsActive);

public sealed record AdminRestaurantListQuery(bool IncludeInactive = true, int Page = 1, int PageSize = 20);

public sealed record OpeningHoursDto(int DayOfWeek, string OpensAt, string ClosesAt);

public sealed record AdminRestaurantDto(
    Guid Id,
    string Name,
    string Description,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    int PriceLevel,
    string? Phone,
    string? ImageUrl,
    string? TimeZoneId,
    bool IsActive,
    bool IsPartner,
    DateTime CreatedAt,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<OpeningHoursDto> OpeningHours,
    int PublicReviewCount);

public sealed record AdminRestaurantListResultDto(
    IReadOnlyList<AdminRestaurantDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
