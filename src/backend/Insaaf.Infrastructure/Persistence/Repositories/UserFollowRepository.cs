using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class UserFollowRepository : IUserFollowRepository
{
    private readonly AppDbContext _dbContext;

    public UserFollowRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.UserFollows.AnyAsync(
            item => item.FollowerId == follow.FollowerId && item.FolloweeId == follow.FolloweeId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        _dbContext.UserFollows.Add(follow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request added the same follow first: the primary key kept it to one row.
            _dbContext.ChangeTracker.Clear();
        }
    }

    public async Task RemoveIfExistsAsync(
        Guid followerId,
        Guid followeeId,
        CancellationToken cancellationToken = default)
    {
        var follow = await _dbContext.UserFollows.SingleOrDefaultAsync(
            item => item.FollowerId == followerId && item.FolloweeId == followeeId,
            cancellationToken);
        if (follow is null)
        {
            return;
        }

        _dbContext.UserFollows.Remove(follow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent request removed it first.
            _dbContext.ChangeTracker.Clear();
        }
    }
}
