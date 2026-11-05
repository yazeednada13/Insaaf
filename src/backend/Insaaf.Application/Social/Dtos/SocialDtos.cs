using System.Text.Json.Serialization;
using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Social.Dtos;

public sealed record ReviewLikeStateDto(Guid ReviewId, int LikeCount, bool IsLikedByMe);

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
    bool IsMine);

public sealed record ReviewCommentListResultDto(
    IReadOnlyList<ReviewCommentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReviewCommentDeletedDto(Guid ReviewId, int CommentCount);
