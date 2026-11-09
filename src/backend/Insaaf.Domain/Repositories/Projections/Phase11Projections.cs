using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories.Projections;

/// <summary>A servable photo of one of the restaurant's public reviews (Phase 11, «صور المجتمع»).</summary>
public sealed record RestaurantPhotoProjection(Guid Id, Guid ReviewId, int Width, int Height, DateTime AttachedAt);

/// <summary>The user's Taste Setup answers (Phase 11).</summary>
public sealed record TastePreferencesProjection(
    IReadOnlyList<CategoryProjection> Categories,
    TastePriority Priorities,
    DateTime? CompletedAt);

/// <summary>In-app notification settings (Phase 11).</summary>
public sealed record NotificationSettingsProjection(bool Likes, bool Comments, bool Follows);

/// <summary>A review report for the Admin queue (Phase 11).</summary>
public sealed record AdminReportProjection(
    Guid Id,
    Guid ReviewId,
    Guid RestaurantId,
    string RestaurantName,
    Guid ReporterUserId,
    string? ReporterUserName,
    ReviewReportReason Reason,
    string? Note,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    bool IsReviewHidden);
