using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Social.Dtos;

public sealed record UserFollowStateDto(Guid UserId, bool IsFollowedByMe);

public sealed record FeedQuery(string? Cursor = null, int Limit = 20);

/// <remarks><c>CreatedAt</c> is UTC.</remarks>
public sealed record FeedItemDto(
    Guid Id,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    ReviewerDto Reviewer,
    ReviewRestaurantDto Restaurant,
    int LikeCount,
    int CommentCount,
    bool IsLikedByMe);

/// <remarks><c>NextCursor</c> is opaque, and null on the last page.</remarks>
public sealed record FeedResultDto(IReadOnlyList<FeedItemDto> Items, string? NextCursor);
