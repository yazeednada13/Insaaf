using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories.Projections;

/// <summary>A restaurant as Admin sees it: active or not, with its raw opening hours.</summary>
public sealed record AdminRestaurantProjection(
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
    string? TimeZoneId,
    bool IsActive,
    bool IsPartner,
    DateTime CreatedAt,
    IReadOnlyList<CategoryProjection> Categories,
    IReadOnlyList<OpeningHoursProjection> OpeningHours,
    int PublicReviewCount);

/// <summary>Validated restaurant data for a create or a full update. Categories and hours replace existing ones.</summary>
public sealed record RestaurantWriteModel(
    string Name,
    string Description,
    string City,
    string? Address,
    double Latitude,
    double Longitude,
    PriceLevel PriceLevel,
    string? Phone,
    string? ImageUrl,
    string? TimeZoneId,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyList<OpeningHoursProjection> OpeningHours);

/// <summary>A review as Admin sees it, hidden or not. Text is included for moderation.</summary>
public sealed record AdminReviewProjection(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    Guid AuthorId,
    string? AuthorUserName,
    int Rating,
    string Text,
    DateTime CreatedAt,
    DateTime? HiddenAt,
    string? HiddenReason,
    int CommentCount);

/// <summary>An account as Admin sees it.</summary>
public sealed record AdminUserProjection(
    Guid Id,
    string? UserName,
    string? FullName,
    string? Email,
    bool IsAdmin,
    DateTime? SuspendedAt);
