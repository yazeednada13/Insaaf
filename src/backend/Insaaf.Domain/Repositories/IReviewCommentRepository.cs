using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

public interface IReviewCommentRepository
{
    /// <summary>A review's comments, oldest first, with author details.</summary>
    Task<PagedResult<ReviewCommentProjection>> ListByReviewAsync(
        Guid reviewId,
        int page,
        int pageSize,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    Task<ReviewCommentProjection?> GetAsync(
        Guid commentId,
        Guid viewerUserId = default,
        CancellationToken cancellationToken = default);

    Task AddAsync(ReviewComment comment, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a comment; false when it no longer exists.</summary>
    Task<bool> DeleteAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
