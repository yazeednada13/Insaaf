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
    private readonly IDateTimeProvider _clock;

    public UserFollowService(IUserFollowRepository follows, IDateTimeProvider clock)
    {
        _follows = follows;
        _clock = clock;
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

        return new UserFollowStateDto(userId, true);
    }

    public async Task<UserFollowStateDto> UnfollowAsync(
        Guid followerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureValidTargetAsync(followerId, userId, cancellationToken);

        await _follows.RemoveIfExistsAsync(followerId, userId, cancellationToken);

        return new UserFollowStateDto(userId, false);
    }

    private async Task EnsureValidTargetAsync(Guid followerId, Guid userId, CancellationToken cancellationToken)
    {
        if (followerId == userId)
        {
            throw new SocialException("cannot_follow_self", "You can't follow yourself.");
        }

        if (!await _follows.UserExistsAsync(userId, cancellationToken))
        {
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }
    }
}
