namespace Insaaf.Domain.Repositories.Projections;

public sealed record FeedItemProjection(
    Guid Id,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    Guid ReviewerId,
    string? ReviewerFullName,
    string? ReviewerUserName,
    Guid RestaurantId,
    string RestaurantName,
    string RestaurantCity,
    string? RestaurantAddress,
    int LikeCount,
    int CommentCount,
    bool IsLikedByViewer);
