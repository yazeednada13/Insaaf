using System.Text.Json.Serialization;

namespace Insaaf.Application.Admin.Dtos;

public sealed record AdminReviewListQuery(Guid? RestaurantId = null, Guid? UserId = null, string? Cursor = null, int Limit = 20);

/// <summary>A review for moderation, hidden or not. Admin endpoints only.</summary>
public sealed record AdminReviewDto(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    Guid AuthorId,
    string? AuthorUserName,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsHidden,
    DateTime? HiddenAt,
    string? HiddenReason,
    int CommentCount);

public sealed record AdminReviewListResultDto(IReadOnlyList<AdminReviewDto> Items, string? NextCursor);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetReviewVisibilityRequest(bool? Hidden, string? Reason);

public sealed record AdminCommentDeletedDto(Guid CommentId, bool Deleted);

/// <summary>An account for moderation. Admin endpoints only.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string? UserName,
    string? FullName,
    string? Email,
    bool IsAdmin,
    bool IsSuspended,
    DateTime? SuspendedAt);

public sealed record AdminUserSearchResultDto(IReadOnlyList<AdminUserDto> Items);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetSuspensionRequest(bool? Suspended);
