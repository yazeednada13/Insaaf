using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Entities;

/// <summary>
/// A user's report of a review (Phase 11). At most one open report per user per review; an Admin
/// resolves it. Reporting never changes the review: hiding stays a separate Admin action.
/// </summary>
public sealed class ReviewReport
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }

    /// <summary>The reporting Identity user.</summary>
    public Guid ReporterUserId { get; set; }

    public ReviewReportReason Reason { get; set; }

    /// <summary>Optional free text. Never written to logs.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>The Admin who resolved it. No foreign key, like the other moderation actors (ADR-017).</summary>
    public Guid? ResolvedByUserId { get; set; }
}
