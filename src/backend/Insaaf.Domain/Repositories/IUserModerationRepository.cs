using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

/// <summary>Account lookups and suspension for Admin (Phase 9). Suspension never touches the lockout fields.</summary>
public interface IUserModerationRepository
{
    /// <summary>Accounts whose email or username contains <paramref name="query"/>, by username.</summary>
    Task<IReadOnlyList<AdminUserProjection>> SearchAsync(string query, int take, CancellationToken cancellationToken = default);

    Task<AdminUserProjection?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Null when the account doesn't exist; otherwise whether it changed.</summary>
    Task<bool?> SetSuspendedAsync(
        Guid userId,
        bool suspended,
        Guid adminId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
