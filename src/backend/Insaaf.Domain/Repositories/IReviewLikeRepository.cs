using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

public interface IReviewLikeRepository
{
    /// <summary>Adds the like if it isn't there; a repeat or a concurrent duplicate is a no-op.</summary>
    Task AddIfMissingAsync(ReviewLike like, CancellationToken cancellationToken = default);

    /// <summary>Removes the like if it exists.</summary>
    Task RemoveIfExistsAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
