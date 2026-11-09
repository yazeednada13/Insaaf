using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

/// <summary>Official restaurant replies (Phase 11): at most one per review.</summary>
public interface IReviewReplyRepository
{
    /// <summary>Creates or replaces the reply. Returns true when it was created.</summary>
    Task<bool> UpsertAsync(Guid reviewId, string text, Guid adminId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<ReviewReply?> GetAsync(Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>False when there was no reply.</summary>
    Task<bool> DeleteAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
