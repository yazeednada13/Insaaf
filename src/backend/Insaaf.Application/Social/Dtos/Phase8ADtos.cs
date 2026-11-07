using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Social.Dtos;

public sealed record NotificationsQuery(string? Cursor = null, int Limit = 20);

/// <remarks><c>Type</c> is like, comment or follow; the device builds the text. <c>CreatedAt</c> is UTC.</remarks>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    ReviewerDto Actor,
    Guid? ReviewId,
    DateTime CreatedAt,
    bool IsRead);

public sealed record NotificationListResultDto(IReadOnlyList<NotificationDto> Items, string? NextCursor);

public sealed record UnreadCountDto(int Count);

public sealed record HomeRestaurantsQuery(int Limit = 10);

public sealed record HomeRestaurantsDto(IReadOnlyList<RestaurantListItemDto> Items);

/// <remarks><c>Share</c> is a percentage; <c>Level</c> is very_high, high or medium.</remarks>
public sealed record CategoryTasteDto(string Name, string Slug, int Share, string Level);

public sealed record TasteDto(int ReviewCount, bool HasEnoughReviews, IReadOnlyList<CategoryTasteDto> Categories);

public sealed record CategoryMatchDto(string Name, string Slug, int Percentage);

/// <remarks><c>Percentage</c> is null when <c>Available</c> is false.</remarks>
public sealed record TasteMatchDto(bool Available, int? Percentage, IReadOnlyList<CategoryMatchDto> Categories);
