using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface IUserProfileService
{
    Task<UserProfileDto> GetAsync(Guid userId, Guid viewerUserId, CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpdateMineAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<FeedResultDto> ListReviewsAsync(
        Guid userId,
        Guid viewerUserId,
        FeedQuery query,
        CancellationToken cancellationToken = default);

    Task<UserListResultDto> ListFollowersAsync(
        Guid userId,
        Guid viewerUserId,
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<UserListResultDto> ListFollowingAsync(
        Guid userId,
        Guid viewerUserId,
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<UserSearchResultDto> SearchAsync(
        Guid viewerUserId,
        UserSearchQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>Public profiles. Admin accounts behave as unknown users, except for their own profile.</summary>
public sealed class UserProfileService : IUserProfileService
{
    public const int MaxFullNameLength = 256;
    public const int MaxBioLength = 150;
    public const int MinSearchLength = 2;
    public const int MaxSearchLength = 100;
    public const int MaxPageSize = 50;

    private readonly IUserProfileRepository _profiles;
    private readonly IReviewRepository _reviews;
    private readonly IUserAccountService _accounts;

    public UserProfileService(
        IUserProfileRepository profiles,
        IReviewRepository reviews,
        IUserAccountService accounts)
    {
        _profiles = profiles;
        _reviews = reviews;
        _accounts = accounts;
    }

    public async Task<UserProfileDto> GetAsync(
        Guid userId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetPublicAsync(userId, viewerUserId, cancellationToken)
            ?? throw UserNotFound();

        return ToDto(profile, viewerUserId);
    }

    public async Task<UserProfileDto> GetMineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetOwnAsync(userId, cancellationToken) ?? throw UserNotFound();
        // Phase 11: the app offers Taste Setup until it's done.
        var taste = await _profiles.GetTastePreferencesAsync(userId, cancellationToken);
        return ToDto(profile, userId) with { HasCompletedTasteSetup = taste?.CompletedAt is not null };
    }

    public async Task<UserProfileDto> UpdateMineAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName?.Trim();
        var userName = request.UserName?.Trim();
        var bio = request.Bio?.Trim();

        if (string.IsNullOrEmpty(fullName) || fullName.Length > MaxFullNameLength)
        {
            throw new SocialException("validation_error", $"Full name is required, up to {MaxFullNameLength} characters.");
        }

        if (string.IsNullOrEmpty(userName))
        {
            throw new SocialException("validation_error", "Username is required.");
        }

        if (bio?.Length > MaxBioLength)
        {
            throw new SocialException("validation_error", $"Bio must be at most {MaxBioLength} characters.");
        }

        var result = await _accounts.UpdateProfileAsync(
            userId,
            fullName,
            userName,
            string.IsNullOrEmpty(bio) ? null : bio,
            cancellationToken);

        return result switch
        {
            ProfileUpdateResult.Updated => await GetMineAsync(userId, cancellationToken),
            ProfileUpdateResult.UsernameTaken => throw new SocialException(
                "username_taken",
                "This username is already taken.",
                SocialStatusCodes.Conflict),
            ProfileUpdateResult.InvalidUsername => throw new SocialException(
                "validation_error",
                "Username contains characters that aren't allowed."),
            _ => throw UserNotFound(),
        };
    }

    public async Task<FeedResultDto> ListReviewsAsync(
        Guid userId,
        Guid viewerUserId,
        FeedQuery query,
        CancellationToken cancellationToken = default)
    {
        FeedCursorCodec.ValidateLimit(query.Limit);
        var cursor = FeedCursorCodec.Decode(query.Cursor);

        // The viewer's own reviews are always reachable, including for an Admin account.
        if (userId != viewerUserId)
        {
            await EnsurePublicAsync(userId, cancellationToken);
        }

        var rows = await _reviews.ListByUserAsync(userId, viewerUserId, cursor, query.Limit + 1, cancellationToken);
        return FeedService.ToResult(rows, query.Limit);
    }

    public async Task<UserListResultDto> ListFollowersAsync(
        Guid userId,
        Guid viewerUserId,
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(query.Page, query.PageSize);
        await EnsureListableAsync(userId, viewerUserId, cancellationToken);

        return ToDto(
            await _profiles.ListFollowersAsync(userId, viewerUserId, query.Page, query.PageSize, cancellationToken),
            viewerUserId);
    }

    public async Task<UserListResultDto> ListFollowingAsync(
        Guid userId,
        Guid viewerUserId,
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(query.Page, query.PageSize);
        await EnsureListableAsync(userId, viewerUserId, cancellationToken);

        return ToDto(
            await _profiles.ListFollowingAsync(userId, viewerUserId, query.Page, query.PageSize, cancellationToken),
            viewerUserId);
    }

    public async Task<UserSearchResultDto> SearchAsync(
        Guid viewerUserId,
        UserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(query.Page, query.PageSize);

        var term = query.Q?.Trim();
        if (term is null || term.Length < MinSearchLength || term.Length > MaxSearchLength)
        {
            throw new SocialException(
                "validation_error",
                $"Search must be between {MinSearchLength} and {MaxSearchLength} characters.");
        }

        var result = await _profiles.SearchAsync(term, query.Page, query.PageSize, cancellationToken);

        return new UserSearchResultDto(
            result.Items
                .Select(item => new UserSearchItemDto(
                    item.Id,
                    item.FullName,
                    item.UserName,
                    item.ReviewCount,
                    item.FollowerCount,
                    item.Id == viewerUserId,
                    UserAvatars.Url(item.Id, item.AvatarKey)))
                .ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    private async Task EnsureListableAsync(Guid userId, Guid viewerUserId, CancellationToken cancellationToken)
    {
        if (userId != viewerUserId)
        {
            await EnsurePublicAsync(userId, cancellationToken);
        }
    }

    private async Task EnsurePublicAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await _profiles.IsPublicAsync(userId, cancellationToken))
        {
            throw UserNotFound();
        }
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new SocialException(
                "validation_error",
                $"Page must be at least 1, and page size between 1 and {MaxPageSize}.");
        }
    }

    private static SocialException UserNotFound() =>
        new("not_found", "User was not found.", SocialStatusCodes.NotFound);

    private static UserProfileDto ToDto(UserProfileProjection profile, Guid viewerUserId) =>
        new(
            profile.Id,
            profile.FullName,
            profile.UserName,
            profile.Bio,
            profile.ReviewCount,
            profile.FollowerCount,
            profile.FollowingCount,
            profile.IsFollowedByViewer,
            profile.Id == viewerUserId,
            UserAvatars.Url(profile.Id, profile.AvatarKey));

    private static UserListResultDto ToDto(PagedResult<UserListItemProjection> result, Guid viewerUserId) =>
        new(
            result.Items
                .Select(item => new UserListItemDto(
                    item.Id,
                    item.FullName,
                    item.UserName,
                    item.IsFollowedByViewer,
                    item.Id == viewerUserId,
                    UserAvatars.Url(item.Id, item.AvatarKey)))
                .ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
}
