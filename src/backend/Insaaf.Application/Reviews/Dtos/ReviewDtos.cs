using Insaaf.Application.Photos.Dtos;

namespace Insaaf.Application.Reviews.Dtos;

/// <remarks><c>Sort</c>: newest (default), highest, lowest.</remarks>
public sealed record RestaurantReviewsQuery(
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);

/// <remarks><c>HasPublicProfile</c> is false for Admin accounts: the name shows, but isn't navigable.</remarks>
/// <remarks><c>AvatarUrl</c> (Phase 11) is a relative path, or null for initials.</remarks>
public sealed record ReviewerDto(Guid Id, string? FullName, string? UserName, bool HasPublicProfile = true, string? AvatarUrl = null);

/// <summary>The restaurant's official reply (Phase 11), shown as «رسمي». <c>CreatedAt</c> is UTC.</summary>
public sealed record ReviewReplyDto(string Text, DateTime CreatedAt);

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
    bool IsLikedByMe,
    IReadOnlyList<ReviewPhotoDto> Photos,
    bool IsSavedByMe = false,
    ReviewReplyDto? RestaurantReply = null);

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
    bool IsLikedByMe,
    bool IsReviewerFollowedByMe,
    IReadOnlyList<ReviewPhotoDto> Photos,
    bool IsSavedByMe = false,
    ReviewReplyDto? RestaurantReply = null);

public sealed record ReviewListResultDto(
    IReadOnlyList<ReviewListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
