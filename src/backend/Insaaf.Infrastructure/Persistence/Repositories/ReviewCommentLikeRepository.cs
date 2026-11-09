using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewCommentLikeRepository : IReviewCommentLikeRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewCommentLikeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddIfMissingAsync(ReviewCommentLike like, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.ReviewCommentLikes.AnyAsync(
                item => item.CommentId == like.CommentId && item.UserId == like.UserId,
                cancellationToken))
        {
            return;
        }

        _dbContext.ReviewCommentLikes.Add(like);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request added the same like first.
            _dbContext.ChangeTracker.Clear();
        }
    }

    public async Task RemoveIfExistsAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var like = await _dbContext.ReviewCommentLikes.SingleOrDefaultAsync(
            item => item.CommentId == commentId && item.UserId == userId,
            cancellationToken);
        if (like is null)
        {
            return;
        }

        _dbContext.ReviewCommentLikes.Remove(like);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
        }
    }

    public Task<int> CountAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewCommentLikes.CountAsync(like => like.CommentId == commentId, cancellationToken);
}
