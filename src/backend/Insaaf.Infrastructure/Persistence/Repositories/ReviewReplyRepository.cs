using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewReplyRepository : IReviewReplyRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewReplyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> UpsertAsync(Guid reviewId, string text, Guid adminId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ReviewReplies.SingleOrDefaultAsync(reply => reply.ReviewId == reviewId, cancellationToken);
        if (existing is not null)
        {
            existing.Text = text;
            existing.WrittenByUserId = adminId;
            existing.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        _dbContext.ReviewReplies.Add(new ReviewReply
        {
            ReviewId = reviewId,
            Text = text,
            WrittenByUserId = adminId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another Admin created it at the same moment: replace theirs instead.
            _dbContext.ChangeTracker.Clear();
            return await UpsertAsync(reviewId, text, adminId, utcNow, cancellationToken);
        }
    }

    public Task<ReviewReply?> GetAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewReplies.AsNoTracking().SingleOrDefaultAsync(reply => reply.ReviewId == reviewId, cancellationToken);

    public async Task<bool> DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var reply = await _dbContext.ReviewReplies.SingleOrDefaultAsync(item => item.ReviewId == reviewId, cancellationToken);
        if (reply is null)
        {
            return false;
        }

        _dbContext.ReviewReplies.Remove(reply);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
