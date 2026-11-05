using System.Text.Json.Serialization;

namespace Insaaf.Application.Social.Dtos;

/// <remarks>Public data only: email and roles never appear here.</remarks>
public sealed record UserProfileDto(
    Guid Id,
    string? FullName,
    string? UserName,
    string? Bio,
    int ReviewCount,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByMe,
    bool IsMe);

/// <remarks>Extra fields are rejected.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateProfileRequest(string? FullName, string? UserName, string? Bio);

public sealed record UserListQuery(int Page = 1, int PageSize = 20);

public sealed record UserListItemDto(
    Guid Id,
    string? FullName,
    string? UserName,
    bool IsFollowedByMe,
    bool IsMe);

public sealed record UserListResultDto(
    IReadOnlyList<UserListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record UserSearchQuery(string? Q = null, int Page = 1, int PageSize = 20);

public sealed record UserSearchItemDto(
    Guid Id,
    string? FullName,
    string? UserName,
    int ReviewCount,
    int FollowerCount,
    bool IsMe);

public sealed record UserSearchResultDto(
    IReadOnlyList<UserSearchItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
