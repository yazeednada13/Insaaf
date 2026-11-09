using Insaaf.Application.Auth.Abstractions;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

/// <summary>Decides who is notified when someone likes, comments or follows.</summary>
public interface INotificationPublisher
{
    Task LikedAsync(Guid reviewId, Guid actorId, CancellationToken cancellationToken = default);

    Task UnlikedAsync(Guid reviewId, Guid actorId, CancellationToken cancellationToken = default);

    Task CommentedAsync(Guid reviewId, Guid commentId, Guid actorId, CancellationToken cancellationToken = default);

    Task CommentDeletedAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task FollowedAsync(Guid actorId, Guid followeeId, CancellationToken cancellationToken = default);

    Task UnfollowedAsync(Guid actorId, Guid followeeId, CancellationToken cancellationToken = default);

    /// <summary>The restaurant officially replied (Phase 11). The actor is the Admin who wrote it.</summary>
    Task RepliedAsync(Guid reviewId, Guid adminId, CancellationToken cancellationToken = default);

    Task ReplyDeletedAsync(Guid reviewId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The review's author is notified of likes and comments, and a user is notified of new followers.
/// Your own actions never notify you, and undoing an action removes its notification.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly INotificationRepository _notifications;
    private readonly IReviewRepository _reviews;
    private readonly IDateTimeProvider _clock;

    public NotificationPublisher(
        INotificationRepository notifications,
        IReviewRepository reviews,
        IDateTimeProvider clock)
    {
        _notifications = notifications;
        _reviews = reviews;
        _clock = clock;
    }

    public async Task LikedAsync(Guid reviewId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var authorId = await _reviews.GetAuthorIdAsync(reviewId, cancellationToken);
        if (authorId is not null && authorId != actorId)
        {
            // One like notification per actor per review, however often they like and unlike.
            await _notifications.AddAsync(
                Create(authorId.Value, actorId, NotificationType.Like, reviewId, commentId: null),
                unique: true,
                cancellationToken);
        }
    }

    public async Task UnlikedAsync(Guid reviewId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var authorId = await _reviews.GetAuthorIdAsync(reviewId, cancellationToken);
        if (authorId is not null)
        {
            await _notifications.RemoveAsync(authorId.Value, actorId, NotificationType.Like, reviewId, cancellationToken);
        }
    }

    public async Task CommentedAsync(
        Guid reviewId,
        Guid commentId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var authorId = await _reviews.GetAuthorIdAsync(reviewId, cancellationToken);
        if (authorId is not null && authorId != actorId)
        {
            await _notifications.AddAsync(
                Create(authorId.Value, actorId, NotificationType.Comment, reviewId, commentId),
                unique: false,
                cancellationToken);
        }
    }

    public Task CommentDeletedAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _notifications.RemoveForCommentAsync(commentId, cancellationToken);

    public Task FollowedAsync(Guid actorId, Guid followeeId, CancellationToken cancellationToken = default) =>
        _notifications.AddAsync(
            Create(followeeId, actorId, NotificationType.Follow, reviewId: null, commentId: null),
            unique: true,
            cancellationToken);

    public Task UnfollowedAsync(Guid actorId, Guid followeeId, CancellationToken cancellationToken = default) =>
        _notifications.RemoveAsync(followeeId, actorId, NotificationType.Follow, reviewId: null, cancellationToken);

    public async Task RepliedAsync(Guid reviewId, Guid adminId, CancellationToken cancellationToken = default)
    {
        var authorId = await _reviews.GetAuthorIdAsync(reviewId, cancellationToken);
        if (authorId is not null && authorId != adminId)
        {
            await _notifications.AddAsync(
                Create(authorId.Value, adminId, NotificationType.Reply, reviewId, commentId: null),
                unique: true,
                cancellationToken);
        }
    }

    public Task ReplyDeletedAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        _notifications.RemoveForReviewAsync(NotificationType.Reply, reviewId, cancellationToken);

    private Notification Create(Guid recipientId, Guid actorId, NotificationType type, Guid? reviewId, Guid? commentId) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            ReviewId = reviewId,
            CommentId = commentId,
            CreatedAt = _clock.UtcNow,
        };
}
