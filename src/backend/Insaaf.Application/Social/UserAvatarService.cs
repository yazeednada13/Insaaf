using Insaaf.Application.Photos;
using Insaaf.Application.Photos.Abstractions;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IUserAvatarService
{
    Task<UserAvatarDto> UploadAsync(Guid userId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task<UserAvatarDto> RemoveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The photo of a non-Admin user, or null.</summary>
    Task<ServablePhoto?> OpenAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Profile photos (Phase 11, Figma «تغيير الصورة»). Uploads go through the same image pipeline as review
/// photos (real images only, metadata removed). Each upload is a new file with a new key; the previous file
/// is deleted after the key changes, and a failed delete only leaves an unreferenced file behind.
/// </summary>
public sealed class UserAvatarService : IUserAvatarService
{
    public const int MaxBytes = ReviewPhotoService.MaxBytes;

    private readonly IUserProfileRepository _profiles;
    private readonly IImageProcessor _processor;
    private readonly IAvatarStorage _storage;

    public UserAvatarService(
        IUserProfileRepository profiles,
        IImageProcessor processor,
        IAvatarStorage storage)
    {
        _profiles = profiles;
        _processor = processor;
        _storage = storage;
    }

    public async Task<UserAvatarDto> UploadAsync(Guid userId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (content.IsEmpty)
        {
            throw new SocialException("validation_error", "A file is required.");
        }

        var processed = _processor.ProcessAvatar(content)
            ?? throw new SocialException("unsupported_media_type", "The file must be a JPEG, PNG or WebP image.", 415);

        var key = await _storage.SaveAsync(processed.Jpeg, cancellationToken);
        var (found, previous) = await _profiles.SetAvatarKeyAsync(userId, key, cancellationToken);
        if (!found)
        {
            await DeleteQuietlyAsync(key);
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }

        if (previous is not null)
        {
            await DeleteQuietlyAsync(previous);
        }

        return new UserAvatarDto(UserAvatars.Url(userId, key));
    }

    public async Task<UserAvatarDto> RemoveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (found, previous) = await _profiles.SetAvatarKeyAsync(userId, null, cancellationToken);
        if (!found)
        {
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }

        if (previous is not null)
        {
            await DeleteQuietlyAsync(previous);
        }

        return new UserAvatarDto(null);
    }

    public async Task<ServablePhoto?> OpenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = await _profiles.GetPublicAvatarKeyAsync(userId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        var content = await _storage.OpenReadAsync(key, cancellationToken);
        return content is null ? null : new ServablePhoto(content, $"\"avatar-{key}\"");
    }

    private async Task DeleteQuietlyAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The key no longer points at it; the file is only an unreferenced leftover.
            // Nothing references it any more, so it is never shown or served.
        }
    }
}
