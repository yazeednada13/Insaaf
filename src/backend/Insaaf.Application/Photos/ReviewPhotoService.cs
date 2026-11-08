using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Photos.Abstractions;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Verification;
using Insaaf.Application.Verification.Abstractions;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Photos;

public interface IReviewPhotoService
{
    /// <summary>
    /// Uploads one logical photo for a visit token. Repeating it with the same client upload id returns
    /// the same photo. Never redeems the token.
    /// </summary>
    Task<PhotoUploadResult> UploadAsync(
        Guid userId,
        string? token,
        Guid? clientUploadId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>The uploader removes their own photo before it's attached to a review.</summary>
    Task<PhotoRemovedDto> DeleteOwnAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>The image of a Ready, attached photo of a public review, or null.</summary>
    Task<ServablePhoto?> OpenServableAsync(Guid photoId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Review photo uploads (Phase 9A, tasks 9A.2 and 9A.3). The token must be one a review could still be
/// created with, and the first upload claims it for the uploader. Each processing attempt writes its own
/// file; the database fence decides which one is served (plan section 6). Controllers pass the caller's
/// id; this service never reads claims or HTTP state.
/// </summary>
public sealed class ReviewPhotoService : IReviewPhotoService
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(60);

    private readonly IVisitTokenRepository _visitTokens;
    private readonly IVisitTokenCodec _codec;
    private readonly IReviewPhotoRepository _photos;
    private readonly IPhotoStorage _storage;
    private readonly IImageProcessor _processor;
    private readonly ReviewPhotoRemoval _removal;
    private readonly IDateTimeProvider _clock;

    public ReviewPhotoService(
        IVisitTokenRepository visitTokens,
        IVisitTokenCodec codec,
        IReviewPhotoRepository photos,
        IPhotoStorage storage,
        IImageProcessor processor,
        ReviewPhotoRemoval removal,
        IDateTimeProvider clock)
    {
        _visitTokens = visitTokens;
        _codec = codec;
        _photos = photos;
        _storage = storage;
        _processor = processor;
        _removal = removal;
        _clock = clock;
    }

    public async Task<PhotoUploadResult> UploadAsync(
        Guid userId,
        string? token,
        Guid? clientUploadId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        if (clientUploadId is not { } uploadId || uploadId == Guid.Empty)
        {
            throw new VerificationException("validation_error", "clientUploadId is required.");
        }

        if (content.IsEmpty)
        {
            throw new VerificationException("validation_error", "A photo file is required.");
        }

        if (content.Length > MaxBytes)
        {
            throw FileTooLarge();
        }

        var now = _clock.UtcNow;
        var visitToken = await VerificationService.GetRedeemableTokenAsync(_visitTokens, _codec, token, now, cancellationToken);

        var existing = await _photos.FindByClientUploadIdAsync(visitToken.Id, userId, uploadId, cancellationToken);
        if (existing is not null)
        {
            return await ContinueExistingAsync(existing, content, cancellationToken);
        }

        if (visitToken.PhotoClaimedByUserId is { } claimant && claimant != userId)
        {
            throw VerificationService.TokenClaimed();
        }

        // Processing happens before any reservation, so a bad file leaves nothing behind.
        var processed = _processor.Process(content) ?? throw UnsupportedImage();
        var leaseId = Guid.NewGuid();
        var photo = new ReviewPhoto
        {
            Id = Guid.NewGuid(),
            ClientUploadId = uploadId,
            VisitTokenId = visitToken.Id,
            UploadedByUserId = userId,
            Status = ReviewPhotoStatus.Pending,
            ContentType = "image/jpeg",
            LeaseId = leaseId,
            LeaseExpiresAt = now + LeaseDuration,
            CreatedAt = now,
            UpdatedAt = now,
        };

        PhotoReservationResult reservation;
        try
        {
            reservation = await _photos.ReserveAsync(photo, now, cancellationToken);
        }
        catch (RepositoryBusyException)
        {
            throw VerificationService.Busy();
        }

        switch (reservation)
        {
            case PhotoReservationResult.Duplicate:
                var repeated = await _photos.FindByClientUploadIdAsync(visitToken.Id, userId, uploadId, cancellationToken)
                    ?? throw UploadInProgress();
                return await ContinueExistingAsync(repeated, content, cancellationToken, takeOver: false);
            case PhotoReservationResult.LimitReached:
                throw new VerificationException(
                    "photo_limit",
                    "A review can have at most 5 photos.",
                    VerificationStatusCodes.Conflict);
            case PhotoReservationResult.Claimed:
                throw VerificationService.TokenClaimed();
            case PhotoReservationResult.TokenUnavailable:
                // Re-check to return the precise token error (used, expired or invalid).
                await VerificationService.GetRedeemableTokenAsync(_visitTokens, _codec, token, _clock.UtcNow, cancellationToken);
                throw VerificationService.UsedToken();
        }

        return await RunAttemptAsync(photo.Id, visitToken.Id, leaseId, processed, cancellationToken);
    }

    public async Task<PhotoRemovedDto> DeleteOwnAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _photos.GetAsync(photoId, cancellationToken);
        if (photo is null
            || photo.UploadedByUserId != userId
            || photo.ReviewId is not null
            || photo.Status is ReviewPhotoStatus.Deleting or ReviewPhotoStatus.Purged)
        {
            throw NotFound();
        }

        string? fileDeletion;
        try
        {
            fileDeletion = await _removal.RemoveAsync(photo, deletedByUserId: null, cancellationToken);
        }
        catch (RepositoryBusyException)
        {
            throw VerificationService.Busy();
        }

        return new PhotoRemovedDto(photoId, true, fileDeletion ?? throw NotFound());
    }

    public async Task<ServablePhoto?> OpenServableAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var key = await _photos.GetServableKeyAsync(photoId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        var content = await _storage.OpenReadAsync(key, cancellationToken);
        return content is null ? null : new ServablePhoto(content, $"\"{key.Replace('/', '-')}\"");
    }

    // A retry of the same logical photo (same token, user and client upload id).
    private async Task<PhotoUploadResult> ContinueExistingAsync(
        ReviewPhoto existing,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken,
        bool takeOver = true)
    {
        switch (existing.Status)
        {
            case ReviewPhotoStatus.Ready:
                return new PhotoUploadResult(ToDto(existing.Id, existing.Width, existing.Height), Created: false);
            case ReviewPhotoStatus.Deleting:
            case ReviewPhotoStatus.Purged:
                throw PhotoRemoved();
        }

        if (!takeOver)
        {
            throw UploadInProgress();
        }

        var processed = _processor.Process(content) ?? throw UnsupportedImage();
        var leaseId = Guid.NewGuid();
        var now = _clock.UtcNow;
        if (!await _photos.TryTakeLeaseAsync(existing.Id, leaseId, now, now + LeaseDuration, cancellationToken))
        {
            throw UploadInProgress();
        }

        return await RunAttemptAsync(existing.Id, existing.VisitTokenId, leaseId, processed, cancellationToken);
    }

    // One fenced attempt (plan section 6): write this attempt's own file, then let the database decide.
    private async Task<PhotoUploadResult> RunAttemptAsync(
        Guid photoId,
        Guid visitTokenId,
        Guid leaseId,
        ProcessedImage processed,
        CancellationToken cancellationToken)
    {
        string key;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(ProcessingTimeout);
            try
            {
                key = await _storage.SaveAttemptAsync(photoId, leaseId, processed.Jpeg, timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                await AbandonAsync(photoId, visitTokenId, leaseId);
                throw new VerificationException(
                    "upload_failed",
                    "The photo could not be saved. Please try again.",
                    VerificationStatusCodes.ServerError);
            }
        }

        if (await _photos.CompleteAsync(
                photoId,
                leaseId,
                key,
                processed.Width,
                processed.Height,
                processed.Jpeg.Length,
                _clock.UtcNow,
                cancellationToken))
        {
            return new PhotoUploadResult(ToDto(photoId, processed.Width, processed.Height), Created: true);
        }

        // The fence refused: the lease was taken over or the photo was removed. This attempt's file is
        // unreferenced and the cleanup sweep deletes it.
        var current = await _photos.GetAsync(photoId, cancellationToken);
        throw current?.Status is ReviewPhotoStatus.Deleting or ReviewPhotoStatus.Purged
            ? PhotoRemoved()
            : UploadInProgress();
    }

    // The failed attempt tombstones its own row and clears its lease in one statement, then cleans up.
    // If any of this fails, the row stays tracked (Pending or Deleting) and cleanup recovers it.
    private async Task AbandonAsync(Guid photoId, Guid visitTokenId, Guid leaseId)
    {
        try
        {
            if (await _photos.AbandonAttemptAsync(photoId, leaseId, _clock.UtcNow, CancellationToken.None))
            {
                await _removal.DeleteFilesAndPurgeAsync(photoId, visitTokenId, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Deliberately swallowed: the original failure is reported, and cleanup finishes the work.
        }
    }

    private static UploadedPhotoDto ToDto(Guid id, int? width, int? height) => new(id, width ?? 0, height ?? 0);

    private static VerificationException FileTooLarge() =>
        new("file_too_large", "A photo must be at most 5 MB.", VerificationStatusCodes.PayloadTooLarge);

    private static VerificationException UnsupportedImage() =>
        new("unsupported_media_type", "Only JPEG, PNG or WebP photos are accepted.", VerificationStatusCodes.UnsupportedMediaType);

    private static VerificationException UploadInProgress() =>
        new("upload_in_progress", "This photo is still uploading. Try again shortly.", VerificationStatusCodes.Conflict);

    private static VerificationException PhotoRemoved() =>
        new("photo_removed", "This photo was removed. Upload it again.", VerificationStatusCodes.Gone);

    private static VerificationException NotFound() =>
        new("not_found", "Photo was not found.", VerificationStatusCodes.NotFound);
}
