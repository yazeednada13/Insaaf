namespace Insaaf.Domain.Repositories.Projections;

public sealed record UserProfileProjection(
    Guid Id,
    string? FullName,
    string? UserName,
    string? Bio,
    int ReviewCount,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByViewer);

public sealed record UserListItemProjection(
    Guid Id,
    string? FullName,
    string? UserName,
    bool IsFollowedByViewer);

public sealed record UserSearchItemProjection(
    Guid Id,
    string? FullName,
    string? UserName,
    int ReviewCount,
    int FollowerCount);
