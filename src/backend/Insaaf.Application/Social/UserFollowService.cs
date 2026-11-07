using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IUserFollowService
{
    /// <summary>Follows a user. Repeating it changes nothing.</summary>
    Task<UserFollowStateDto> FollowAsync(Guid followerId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stops following a user. Repeating it changes nothing.</summary>
    Task<UserFollowStateDto> UnfollowAsync(Guid followerId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class UserFollowService : IUserFollowService
{
    private readonly IUserFollowRepository _follows;
    private readonly IUserProfileRepository _profiles;
    private readonly IDateTimeProvider _clock;
    private readonly INotificationPublisher _notifications;

    public UserFollowService(
        IUserFollowRepository follows,
        IUserProfileRepository profiles,
        IDateTimeProvider clock,
        INotificationPublisher notifications)
    {
        _follows = follows;
        _profiles = profiles;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<UserFollowStateDto> FollowAsync(
        Guid followerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidTargetAsync(followerId, userId, cancellationToken);

        await _follows.AddIfMissingAsync(
            new UserFollow { FollowerId = followerId, FolloweeId = userId, CreatedAt = _clock.UtcNow },
            cancellationToken);
        await _notifications.FollowedAsync(followerId, userId, cancellationToken);

        return new UserFollowStateDto(userId, true);
    }

    public async Task<UserFollowStateDto> UnfollowAsync(
        Guid followerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidTargetAsync(followerId, userId, cancellationToken);

        await _follows.RemoveIfExistsAsync(followerId, userId, cancellationToken);
        await _notifications.UnfollowedAsync(followerId, userId, cancellationToken);

        return new UserFollowStateDto(userId, false);
    }

    private async Task EnsureValidTargetAsync(Guid followerId, Guid userId, CancellationToken cancellationToken)
    {
        if (followerId == userId)
        {
            throw new SocialException("cannot_follow_self", "You can't follow yourself.");
        }

        // Admin accounts aren't public, so they behave as unknown users here.
        if (!await _profiles.IsPublicAsync(userId, cancellationToken))
        {
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }
    }
}
