namespace Insaaf.Application.Reviews.Dtos;

/// <remarks><c>Sort</c>: newest (default), highest, lowest.</remarks>
public sealed record RestaurantReviewsQuery(
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);

public sealed record ReviewerDto(Guid Id, string? FullName, string? UserName);

/// <remarks><c>CreatedAt</c> is UTC.</remarks>
public sealed record ReviewListItemDto(
    Guid Id,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    ReviewerDto Reviewer,
    int LikeCount,
    int CommentCount,
    bool IsLikedByMe);

public sealed record ReviewRestaurantDto(Guid Id, string Name, string City, string? Address);

/// <remarks><c>CreatedAt</c> is UTC.</remarks>
public sealed record ReviewDetailDto(
    Guid Id,
    int Rating,
    int FoodRating,
    int ServiceRating,
    int ValueRating,
    int AtmosphereRating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    ReviewerDto Reviewer,
    ReviewRestaurantDto Restaurant,
    int LikeCount,
    int CommentCount,
    bool IsLikedByMe);

public sealed record ReviewListResultDto(
    IReadOnlyList<ReviewListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
