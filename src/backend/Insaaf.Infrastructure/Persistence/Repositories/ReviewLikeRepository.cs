using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewLikeRepository : IReviewLikeRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewLikeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddIfMissingAsync(ReviewLike like, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.ReviewLikes.AnyAsync(
            item => item.ReviewId == like.ReviewId && item.UserId == like.UserId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        _dbContext.ReviewLikes.Add(like);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request added the same like first: the primary key kept it to one row.
            _dbContext.ChangeTracker.Clear();
        }
    }

    public async Task RemoveIfExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default)
    {
        var like = await _dbContext.ReviewLikes.SingleOrDefaultAsync(
            item => item.ReviewId == reviewId && item.UserId == userId,
            cancellationToken);
        if (like is null)
        {
            return;
        }

        _dbContext.ReviewLikes.Remove(like);

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

    public Task<int> CountAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewLikes.CountAsync(like => like.ReviewId == reviewId, cancellationToken);
}
