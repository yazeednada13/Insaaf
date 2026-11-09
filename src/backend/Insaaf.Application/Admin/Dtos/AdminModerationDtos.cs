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

// Phase 11: reports and official replies.

/// <remarks><c>Status</c>: open (default), resolved or all.</remarks>
public sealed record AdminReportListQuery(string? Status = null, string? Cursor = null, int Limit = 20);

public sealed record AdminReportDto(
    Guid Id,
    Guid ReviewId,
    Guid RestaurantId,
    string RestaurantName,
    Guid ReporterId,
    string? ReporterUserName,
    string Reason,
    string? Note,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    bool IsReviewHidden);

public sealed record AdminReportListResultDto(IReadOnlyList<AdminReportDto> Items, string? NextCursor);

/// <remarks>Extra fields are rejected.</remarks>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SetReviewReplyRequest(string? Text);

public sealed record AdminReviewReplyDto(Guid ReviewId, string Text, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record AdminReplyDeletedDto(Guid ReviewId, bool Deleted);

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
