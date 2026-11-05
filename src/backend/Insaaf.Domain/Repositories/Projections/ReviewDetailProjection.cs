namespace Insaaf.Domain.Repositories.Projections;

public sealed record ReviewDetailProjection(
    Guid Id,
    int Rating,
    int FoodRating,
    int ServiceRating,
    int ValueRating,
    int AtmosphereRating,
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
