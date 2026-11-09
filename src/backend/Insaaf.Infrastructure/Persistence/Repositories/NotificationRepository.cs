using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(AppDbContext dbContext, ILogger<NotificationRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task AddAsync(Notification notification, bool unique, CancellationToken cancellationToken = default) =>
        WriteSafelyAsync(
            async () =>
            {
                if (!await RecipientWantsAsync(notification.RecipientId, notification.Type, cancellationToken))
                {
                    return;
                }

                if (unique && await Matching(
                        notification.RecipientId,
                        notification.ActorId,
                        notification.Type,
                        notification.ReviewId).AnyAsync(cancellationToken))
                {
                    return;
                }

                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync(cancellationToken);
            },
            "add");

    public Task RemoveAsync(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? reviewId,
        CancellationToken cancellationToken = default) =>
        WriteSafelyAsync(
            async () =>
            {
                _dbContext.Notifications.RemoveRange(
                    await Matching(recipientId, actorId, type, reviewId).ToListAsync(cancellationToken));
                await _dbContext.SaveChangesAsync(cancellationToken);
            },
            "remove");

    public Task RemoveForCommentAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        WriteSafelyAsync(
            async () =>
            {
                _dbContext.Notifications.RemoveRange(await _dbContext.Notifications
                    .Where(notification => notification.CommentId == commentId)
                    .ToListAsync(cancellationToken));
                await _dbContext.SaveChangesAsync(cancellationToken);
            },
            "remove for comment");

    public Task RemoveForReviewAsync(NotificationType type, Guid reviewId, CancellationToken cancellationToken = default) =>
        WriteSafelyAsync(
            async () =>
            {
                _dbContext.Notifications.RemoveRange(await _dbContext.Notifications
                    .Where(notification => notification.Type == type && notification.ReviewId == reviewId)
                    .ToListAsync(cancellationToken));
                await _dbContext.SaveChangesAsync(cancellationToken);
            },
            "remove for review");

    public async Task<IReadOnlyList<NotificationProjection>> ListAsync(
        Guid recipientId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default)
    {
        var notifications = Visible(recipientId);

        if (after is not null)
        {
            notifications = notifications.Where(notification =>
                notification.CreatedAt < after.CreatedAt
                || (notification.CreatedAt == after.CreatedAt && notification.Id < after.Id));
        }

        return await notifications
            .Join(
                _dbContext.Users,
                notification => notification.ActorId,
                user => user.Id,
                (notification, user) => new { Notification = notification, User = user })
            .OrderByDescending(row => row.Notification.CreatedAt)
            .ThenByDescending(row => row.Notification.Id)
            .Take(take)
            .Select(row => new NotificationProjection(
                row.Notification.Id,
                row.Notification.Type,
                row.User.Id,
                row.User.FullName,
                row.User.UserName,
                !_dbContext.AdminUserIds().Contains(row.User.Id),
                row.Notification.ReviewId,
                row.Notification.CreatedAt,
                row.Notification.ReadAt,
                _dbContext.AdminUserIds().Contains(row.User.Id) ? null : row.User.AvatarKey,
                _dbContext.Reviews.Where(review => review.Id == row.Notification.ReviewId).Select(review => (Guid?)review.RestaurantId).FirstOrDefault(),
                _dbContext.Reviews.Where(review => review.Id == row.Notification.ReviewId).Select(review => review.Restaurant.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        Visible(recipientId).CountAsync(notification => notification.ReadAt == null, cancellationToken);

    public async Task MarkAllReadAsync(Guid recipientId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(notification => notification.RecipientId == recipientId && notification.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.ReadAt = readAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // A notification about a review that's no longer public, or whose restaurant is inactive, is hidden.
    // Phase 11: in-app notification settings. A missing user counts as "on" (the insert then fails safely).
    private async Task<bool> RecipientWantsAsync(Guid recipientId, NotificationType type, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == recipientId)
            .Select(user => new { user.NotifyLikes, user.NotifyComments, user.NotifyFollows })
            .SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return true;
        }

        return type switch
        {
            NotificationType.Like => settings.NotifyLikes,
            NotificationType.Comment => settings.NotifyComments,
            NotificationType.Follow => settings.NotifyFollows,
            _ => true,
        };
    }

    private IQueryable<Notification> Visible(Guid recipientId) =>
        _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientId == recipientId)
            .Where(notification => notification.ReviewId == null
                || _dbContext.PublicReviewsOfActiveRestaurants().Any(review => review.Id == notification.ReviewId));

    private IQueryable<Notification> Matching(Guid recipientId, Guid actorId, NotificationType type, Guid? reviewId) =>
        _dbContext.Notifications.Where(notification =>
            notification.RecipientId == recipientId
            && notification.ActorId == actorId
            && notification.Type == type
            && notification.ReviewId == reviewId);

    private async Task WriteSafelyAsync(Func<Task> write, string operation)
    {
        try
        {
            await write();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The action that caused it is already saved; a lost notification must not undo it.
            _dbContext.ChangeTracker.Clear();
            _logger.LogWarning(exception, "Notification {Operation} failed.", operation);
        }
    }
}
