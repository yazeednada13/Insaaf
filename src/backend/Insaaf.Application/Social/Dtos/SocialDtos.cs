using System.Text.Json.Serialization;
using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Social.Dtos;

public sealed record ReviewLikeStateDto(Guid ReviewId, int LikeCount, bool IsLikedByMe);

/// <summary>
/// A report of a review (Phase 11). <c>Reason</c>: offensive, not_genuine, wrong_information or other.
/// Extra fields are rejected.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportReviewRequest(string? Reason, string? Note);

/// <summary><c>Reported</c> is always true: a repeat while the first report is open changes nothing.</summary>
public sealed record ReviewReportedDto(Guid ReviewId, bool Reported);

/// <summary>A restaurant's or review's saved state for the caller (Phase 11).</summary>
public sealed record SavedStateDto(Guid Id, bool IsSavedByMe);

public sealed record SavedRestaurantsQuery(int Page = 1, int PageSize = 20);

public sealed record SavedRestaurantsResultDto(
    IReadOnlyList<Insaaf.Application.Restaurants.Dtos.RestaurantListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReviewCommentsQuery(int Page = 1, int PageSize = 20);

/// <remarks>Extra fields are rejected, so a comment carries nothing but its text.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateReviewCommentRequest(string? Text);

/// <remarks><c>CreatedAt</c> is UTC.</remarks>
public sealed record ReviewCommentDto(
    Guid Id,
    string Text,
    DateTime CreatedAt,
    ReviewerDto Author,
    bool IsMine,
    int LikeCount = 0,
    bool IsLikedByMe = false);

/// <summary>A comment's like state after a like or unlike (Phase 11).</summary>
public sealed record ReviewCommentLikeStateDto(Guid CommentId, int LikeCount, bool IsLikedByMe);

public sealed record ReviewCommentListResultDto(
    IReadOnlyList<ReviewCommentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReviewCommentDeletedDto(Guid ReviewId, int CommentCount);
