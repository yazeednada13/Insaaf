using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface INotificationService
{
    Task<NotificationListResultDto> ListAsync(
        Guid recipientId,
        NotificationsQuery query,
        CancellationToken cancellationToken = default);

    Task<UnreadCountDto> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<UnreadCountDto> MarkAllReadAsync(Guid recipientId, CancellationToken cancellationToken = default);
}

/// <summary>The caller's own notifications only: the recipient always comes from the JWT.</summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IDateTimeProvider _clock;

    public NotificationService(INotificationRepository notifications, IDateTimeProvider clock)
    {
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<NotificationListResultDto> ListAsync(
        Guid recipientId,
        NotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        FeedCursorCodec.ValidateLimit(query.Limit);
        var cursor = FeedCursorCodec.Decode(query.Cursor);

        // One extra row shows whether another page exists.
        var rows = await _notifications.ListAsync(recipientId, cursor, query.Limit + 1, cancellationToken);
        var page = rows.Take(query.Limit).ToList();

        return new NotificationListResultDto(
            page.Select(ToDto).ToList(),
            rows.Count > query.Limit ? FeedCursorCodec.Encode(page[^1].CreatedAt, page[^1].Id) : null);
    }

    public async Task<UnreadCountDto> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        new(await _notifications.CountUnreadAsync(recipientId, cancellationToken));

    public async Task<UnreadCountDto> MarkAllReadAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        await _notifications.MarkAllReadAsync(recipientId, _clock.UtcNow, cancellationToken);
        return new UnreadCountDto(0);
    }

    private static NotificationDto ToDto(NotificationProjection notification) =>
        new(
            notification.Id,
            notification.Type switch
            {
                NotificationType.Like => "like",
                NotificationType.Comment => "comment",
                _ => "follow",
            },
            new ReviewerDto(
                notification.ActorId,
                notification.ActorFullName,
                notification.ActorUserName,
                notification.ActorHasPublicProfile),
            notification.ReviewId,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
            notification.ReadAt is not null);
}
