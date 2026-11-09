namespace Insaaf.Domain.Repositories.Projections;

public sealed record ReviewListItemProjection(
    Guid Id,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    Guid ReviewerId,
    string? ReviewerFullName,
    string? ReviewerUserName,
    int LikeCount = 0,
    int CommentCount = 0,
    bool IsLikedByViewer = false,
    bool ReviewerHasPublicProfile = true,
    IReadOnlyList<ReviewPhotoProjection>? Photos = null,
    bool IsSavedByViewer = false,
    ReviewReplyProjection? Reply = null,
    string? ReviewerAvatarKey = null);
