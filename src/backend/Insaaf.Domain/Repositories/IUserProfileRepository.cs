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

    /// <summary>Sets or clears the profile photo key (Phase 11). Returns the previous key; null user → false.</summary>
    Task<(bool Found, string? PreviousKey)> SetAvatarKeyAsync(Guid userId, string? avatarKey, CancellationToken cancellationToken = default);

    /// <summary>The user's Taste Setup answers (Phase 11); null when the user doesn't exist.</summary>
    Task<TastePreferencesProjection?> GetTastePreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the chosen categories and priorities and marks Taste Setup complete (Phase 11).</summary>
    Task<bool> SaveTastePreferencesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> categoryIds,
        Insaaf.Domain.Enums.TastePriority priorities,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// People to follow (Phase 11, Taste Setup step 3): active, non-Admin users with public reviews, whom the
    /// viewer doesn't follow yet, most reviews first.
    /// </summary>
    Task<IReadOnlyList<UserSearchItemProjection>> ListSuggestedAsync(Guid viewerUserId, int take, CancellationToken cancellationToken = default);

    Task<NotificationSettingsProjection?> GetNotificationSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> SetNotificationSettingsAsync(Guid userId, NotificationSettingsProjection settings, CancellationToken cancellationToken = default);

    /// <summary>The photo key of a non-Admin user, or null (Phase 11).</summary>
    Task<string?> GetPublicAvatarKeyAsync(Guid userId, CancellationToken cancellationToken = default);

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

    /// <summary>Review count and per-category counts and averages, from public reviews of active restaurants.</summary>
    Task<TasteProjection> GetTasteAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Non-Admin users whose name or username contains the term.</summary>
    Task<PagedResult<UserSearchItemProjection>> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
