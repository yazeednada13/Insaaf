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
    bool IsMe,
    string? AvatarUrl = null,
    bool? HasCompletedTasteSetup = null);

/// <remarks>Extra fields are rejected.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateProfileRequest(string? FullName, string? UserName, string? Bio);

public sealed record UserListQuery(int Page = 1, int PageSize = 20);

/// <summary>
/// Taste Setup answers (Phase 11). <c>Priorities</c>: food_quality, service, price, atmosphere, location.
/// Extra fields are rejected.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetTastePreferencesRequest(IReadOnlyList<string>? CategorySlugs, IReadOnlyList<string>? Priorities);

public sealed record TastePreferencesDto(
    IReadOnlyList<string> CategorySlugs,
    IReadOnlyList<string> Priorities,
    bool HasCompletedTasteSetup);

public sealed record SuggestedUsersQuery(int Limit = 10);

public sealed record SuggestedUsersDto(IReadOnlyList<UserSearchItemDto> Items);

/// <summary>In-app notification settings (Phase 11, Settings «الإشعارات»). Extra fields are rejected.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotificationSettingsDto(bool NotifyLikes, bool NotifyComments, bool NotifyFollows);

/// <summary>The caller's profile photo after an upload or removal (Phase 11).</summary>
public sealed record UserAvatarDto(string? AvatarUrl);

public sealed record UserListItemDto(
    Guid Id,
    string? FullName,
    string? UserName,
    bool IsFollowedByMe,
    bool IsMe,
    string? AvatarUrl = null);

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
    bool IsMe,
    string? AvatarUrl = null);

public sealed record UserSearchResultDto(
    IReadOnlyList<UserSearchItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
