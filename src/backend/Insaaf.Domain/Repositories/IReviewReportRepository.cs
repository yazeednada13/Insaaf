using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

/// <summary>Review reports (Phase 11).</summary>
public interface IReviewReportRepository
{
    /// <summary>Adds the report unless the reporter already has an open one on this review (race-safe).</summary>
    Task<bool> AddIfNoOpenAsync(ReviewReport report, CancellationToken cancellationToken = default);

    /// <summary>Reports, newest first; <paramref name="open"/> null means all.</summary>
    Task<IReadOnlyList<AdminReportProjection>> ListAsync(
        bool? open,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Null when it doesn't exist; false when it was already resolved.</summary>
    Task<bool?> ResolveAsync(Guid reportId, Guid adminId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<AdminReportProjection?> GetAsync(Guid reportId, CancellationToken cancellationToken = default);
}
