using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Photos.Abstractions;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Photos;

/// <summary>
/// Removes a photo safely (Phase 9A plan section 8): tombstone first, so it's invisible at once; then
/// delete its folder; then purge the row, releasing its slot and claim. A row is never purged before its
/// folder is confirmed empty. Anything that fails stays Deleting and the cleanup service finishes it.
/// </summary>
public sealed class ReviewPhotoRemoval
{
    private readonly IReviewPhotoRepository _photos;
    private readonly IPhotoStorage _storage;
    private readonly IDateTimeProvider _clock;

    public ReviewPhotoRemoval(IReviewPhotoRepository photos, IPhotoStorage storage, IDateTimeProvider clock)
    {
        _photos = photos;
        _storage = storage;
        _clock = clock;
    }

    /// <summary>
    /// The file-deletion state, or null when the photo wasn't Pending or Ready (missing or already removed).
    /// </summary>
    public async Task<string?> RemoveAsync(ReviewPhoto photo, Guid? deletedByUserId, CancellationToken cancellationToken = default)
    {
        if (!await _photos.TombstoneAsync(photo.Id, photo.VisitTokenId, deletedByUserId, _clock.UtcNow, cancellationToken))
        {
            return null;
        }

        return await DeleteFilesAndPurgeAsync(photo.Id, photo.VisitTokenId, cancellationToken)
            ? FileDeletionState.Completed
            : FileDeletionState.Pending;
    }

    /// <summary>
    /// Deletes a Deleting photo's folder, then purges its row. True when the folder was confirmed gone,
    /// even if the purge itself must be retried by cleanup.
    /// </summary>
    public async Task<bool> DeleteFilesAndPurgeAsync(Guid photoId, Guid visitTokenId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _storage.DeleteFolderAsync(photoId, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        try
        {
            await _photos.PurgeAsync(photoId, visitTokenId, _clock.UtcNow, cancellationToken);
        }
        catch (RepositoryBusyException)
        {
            // The row stays Deleting; cleanup purges it on its next run.
        }

        return true;
    }
}
