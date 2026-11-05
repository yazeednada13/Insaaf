using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewCommentRepository : IReviewCommentRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewCommentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ReviewCommentProjection>> ListByReviewAsync(
        Guid reviewId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var comments = _dbContext.ReviewComments
            .AsNoTracking()
            .Where(comment => comment.ReviewId == reviewId);

        var totalCount = await comments.CountAsync(cancellationToken);

        // Ends with Id so paging is stable when timestamps tie.
        var items = await WithAuthor(comments
                .OrderBy(comment => comment.CreatedAt)
                .ThenBy(comment => comment.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewCommentProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<ReviewCommentProjection?> GetAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        WithAuthor(_dbContext.ReviewComments.AsNoTracking().Where(comment => comment.Id == commentId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(ReviewComment comment, CancellationToken cancellationToken = default)
    {
        _dbContext.ReviewComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.ReviewComments.FindAsync([commentId], cancellationToken);
        if (comment is null)
        {
            return false;
        }

        _dbContext.ReviewComments.Remove(comment);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent request deleted it first.
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public Task<int> CountAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewComments.CountAsync(comment => comment.ReviewId == reviewId, cancellationToken);

    private IQueryable<ReviewCommentProjection> WithAuthor(IQueryable<ReviewComment> comments) =>
        comments.Join(
            _dbContext.Users,
            comment => comment.UserId,
            user => user.Id,
            (comment, user) => new ReviewCommentProjection(
                comment.Id,
                comment.ReviewId,
                comment.Text,
                comment.CreatedAt,
                user.Id,
                user.FullName,
                user.UserName));
}
