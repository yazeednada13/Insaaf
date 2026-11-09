namespace Insaaf.Domain.Repositories.Projections;

public sealed record ReviewCommentProjection(
    Guid Id,
    Guid ReviewId,
    string Text,
    DateTime CreatedAt,
    Guid AuthorId,
    string? AuthorFullName,
    string? AuthorUserName,
    bool AuthorHasPublicProfile = true,
    string? AuthorAvatarKey = null,
    int LikeCount = 0,
    bool IsLikedByViewer = false);
