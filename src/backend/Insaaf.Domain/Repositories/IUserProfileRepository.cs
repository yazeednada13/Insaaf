using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

public interface IUserProfileRepository
{
    /// <summary>A public profile; null for unknown users and Admin accounts.</summary>
    Task<UserProfileProjection?> GetPublicAsync(
        Guid userId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own profile, including Admin accounts.</summary>
    Task<UserProfileProjection?> GetOwnAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>True when the user exists and isn't an Admin account.</summary>
    Task<bool> IsPublicAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's followers, newest follow first.</summary>
    Task<PagedResult<UserListItemProjection>> ListFollowersAsync(
        Guid userId,
        Guid viewerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Who the user follows, newest follow first.</summary>
    Task<PagedResult<UserListItemProjection>> ListFollowingAsync(
        Guid userId,
        Guid viewerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Non-Admin users whose name or username contains the term.</summary>
    Task<PagedResult<UserSearchItemProjection>> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
