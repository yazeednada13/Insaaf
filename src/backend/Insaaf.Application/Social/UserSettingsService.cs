using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface IUserSettingsService
{
    Task<NotificationSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<NotificationSettingsDto> SetAsync(Guid userId, NotificationSettingsDto? request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification settings (Phase 11, D-6). They control which in-app notifications are created from now on;
/// existing ones stay. The official restaurant reply always notifies.
/// </summary>
public sealed class UserSettingsService : IUserSettingsService
{
    private readonly IUserProfileRepository _profiles;

    public UserSettingsService(IUserProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<NotificationSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await _profiles.GetNotificationSettingsAsync(userId, cancellationToken) ?? throw NotFound();
        return new NotificationSettingsDto(settings.Likes, settings.Comments, settings.Follows);
    }

    public async Task<NotificationSettingsDto> SetAsync(Guid userId, NotificationSettingsDto? request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new SocialException("validation_error", "notifyLikes, notifyComments and notifyFollows are required.");
        }

        if (!await _profiles.SetNotificationSettingsAsync(
                userId,
                new NotificationSettingsProjection(request.NotifyLikes, request.NotifyComments, request.NotifyFollows),
                cancellationToken))
        {
            throw NotFound();
        }

        return await GetAsync(userId, cancellationToken);
    }

    private static SocialException NotFound() => new("not_found", "User was not found.", SocialStatusCodes.NotFound);
}
