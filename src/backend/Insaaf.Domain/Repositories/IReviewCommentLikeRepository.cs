using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

/// <summary>Comment likes (Phase 11). Repeat-safe and race-safe, like review likes.</summary>
public interface IReviewCommentLikeRepository
{
    Task AddIfMissingAsync(ReviewCommentLike like, CancellationToken cancellationToken = default);

    Task RemoveIfExistsAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid commentId, CancellationToken cancellationToken = default);
}
