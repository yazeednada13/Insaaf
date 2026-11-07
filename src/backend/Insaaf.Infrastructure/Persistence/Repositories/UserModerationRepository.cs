using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Admin account lookups and suspension (Phase 9). Suspension uses its own columns and never changes
/// Identity's LockoutEnd or AccessFailedCount, so a failed-login lockout survives an unsuspend.
/// </summary>
public sealed class UserModerationRepository : IUserModerationRepository
{
    private readonly AppDbContext _dbContext;

    public UserModerationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminUserProjection>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken = default)
    {
        var term = query.Trim().ToUpperInvariant();
        // Ordered before the projection: EF can't sort on a positional record's members.
        return await Items(_dbContext.Users.AsNoTracking()
                .Where(user =>
                    (user.NormalizedEmail != null && user.NormalizedEmail.Contains(term))
                    || (user.NormalizedUserName != null && user.NormalizedUserName.Contains(term)))
                .OrderBy(user => user.UserName)
                .ThenBy(user => user.Id)
                .Take(take))
            .ToListAsync(cancellationToken);
    }

    public Task<AdminUserProjection?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Items(_dbContext.Users.AsNoTracking().Where(user => user.Id == userId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool?> SetSuspendedAsync(
        Guid userId,
        bool suspended,
        Guid adminId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if ((user.SuspendedAt is not null) == suspended)
        {
            return false;
        }

        user.SuspendedAt = suspended ? utcNow : null;
        user.SuspendedByUserId = suspended ? adminId : null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<AdminUserProjection> Items(IQueryable<Identity.ApplicationUser> users) =>
        users.Select(user => new AdminUserProjection(
            user.Id,
            user.UserName,
            user.FullName,
            user.Email,
            _dbContext.AdminUserIds().Contains(user.Id),
            user.SuspendedAt));
}
