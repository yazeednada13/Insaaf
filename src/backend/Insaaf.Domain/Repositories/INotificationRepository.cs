using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Repositories;

/// <summary>
/// Notification storage. Writes never fail the action that caused them: an error is logged and the
/// like, comment or follow still stands.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Adds the notification; with <paramref name="unique"/>, skips it when an equal one exists. Phase 11: also
    /// skipped when the recipient turned that type off (likes, comments, follows; replies are always on).
    /// </summary>
    Task AddAsync(Notification notification, bool unique, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? reviewId,
        CancellationToken cancellationToken = default);

    Task RemoveForCommentAsync(Guid commentId, CancellationToken cancellationToken = default);

    /// <summary>Removes every notification of one type about one review (Phase 11: a deleted reply).</summary>
    Task RemoveForReviewAsync(NotificationType type, Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>The recipient's notifications, newest first; review notifications only for public reviews.</summary>
    Task<IReadOnlyList<NotificationProjection>> ListAsync(
        Guid recipientId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(Guid recipientId, DateTime readAt, CancellationToken cancellationToken = default);
}
