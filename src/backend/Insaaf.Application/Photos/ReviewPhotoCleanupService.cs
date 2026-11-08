using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Photos.Abstractions;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Photos;

public interface IReviewPhotoCleanupService
{
    Task<PhotoCleanupReport> RunOnceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Recovers every photo that a request left unfinished (Phase 9A plan section 8). Each item is handled
/// on its own, so one failure never blocks the rest; anything that fails is retried on the next run.
/// </summary>
public sealed class ReviewPhotoCleanupService : IReviewPhotoCleanupService
{
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromHours(1);
    public static readonly TimeSpan UnattachedLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan LeftoverFileAge = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan OrphanFolderAge = TimeSpan.FromHours(1);
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromDays(30);

    private const int BatchSize = 200;

    private readonly IReviewPhotoRepository _photos;
    private readonly IPhotoStorage _storage;
    private readonly ReviewPhotoRemoval _removal;
    private readonly IDateTimeProvider _clock;

    public ReviewPhotoCleanupService(
        IReviewPhotoRepository photos,
        IPhotoStorage storage,
        ReviewPhotoRemoval removal,
        IDateTimeProvider clock)
    {
        _photos = photos;
        _storage = storage;
        _removal = removal;
        _clock = clock;
    }

    public async Task<PhotoCleanupReport> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        int tombstoned = 0, purged = 0, filesSwept = 0, foldersSwept = 0, pruned = 0, failures = 0;

        // 1–2. Unfinished and abandoned uploads become Deleting.
        var stale = await _photos.ListStalePendingAsync(now - PendingTimeout, now, BatchSize, cancellationToken);
        var expired = await _photos.ListExpiredUnattachedAsync(now - UnattachedLifetime, now, BatchSize, cancellationToken);
        foreach (var photo in stale.Concat(expired))
        {
            if (await TryAsync(() => _photos.TombstoneAsync(photo.Id, photo.VisitTokenId, null, now, cancellationToken)) is true)
            {
                tombstoned++;
            }
            else
            {
                failures++;
            }
        }

        // 3. Deleting rows: folder first, then purge.
        foreach (var photo in await _photos.ListDeletingAsync(BatchSize, cancellationToken))
        {
            if (await TryAsync(() => _removal.DeleteFilesAndPurgeAsync(photo.Id, photo.VisitTokenId, cancellationToken)) is true)
            {
                purged++;
            }
            else
            {
                failures++;
            }
        }

        // 4. Sweep files no row serves: superseded attempts, late writers, crashed temp files.
        var folders = await _storage.ListFoldersAsync(cancellationToken);
        var rows = (await _photos.ListByIdsAsync(folders.Select(folder => folder.PhotoId).ToList(), cancellationToken))
            .ToDictionary(photo => photo.Id);
        foreach (var folder in folders)
        {
            rows.TryGetValue(folder.PhotoId, out var row);
            var result = await TryAsync(() => SweepFolderAsync(folder, row, now, cancellationToken));
            if (result is null)
            {
                failures++;
            }
            else if (result.Value < 0)
            {
                foldersSwept++;
            }
            else
            {
                filesSwept += result.Value;
            }
        }

        // 5. Tombstones older than the retention, once their folder is confirmed empty.
        foreach (var photo in await _photos.ListPrunableAsync(now - TombstoneRetention, BatchSize, cancellationToken))
        {
            var done = await TryAsync(async () =>
            {
                await _storage.DeleteFolderAsync(photo.Id, cancellationToken);
                return await _photos.PruneAsync(photo.Id, cancellationToken);
            });
            if (done is true)
            {
                pruned++;
            }
            else
            {
                failures++;
            }
        }

        // 6. Safety net for the slot invariant.
        var repaired = await TryAsync(() => _photos.RepairTokenCountersAsync(cancellationToken)) ?? 0;

        return new PhotoCleanupReport(tombstoned, purged, filesSwept, foldersSwept, pruned, repaired, failures);
    }

    // Returns -1 when the whole folder was removed, otherwise the number of files removed.
    private async Task<int> SweepFolderAsync(PhotoFolderEntry folder, ReviewPhoto? row, DateTime now, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            if (folder.LastWriteUtc >= now - OrphanFolderAge)
            {
                return 0;
            }

            await _storage.DeleteFolderAsync(folder.PhotoId, cancellationToken);
            return -1;
        }

        if (row.Status is ReviewPhotoStatus.Deleting or ReviewPhotoStatus.Purged)
        {
            var files = await _storage.ListFolderAsync(folder.PhotoId, cancellationToken);
            if (files.Count == 0)
            {
                return 0;
            }

            await _storage.DeleteFolderAsync(folder.PhotoId, cancellationToken);
            return -1;
        }

        // Ready keeps its served file; Pending keeps the current attempt's file and temp file.
        var keep = row.Status == ReviewPhotoStatus.Ready
            ? row.StorageKey
            : row.LeaseId is { } lease ? _storage.KeyFor(row.Id, lease) : null;
        var removed = 0;
        foreach (var file in await _storage.ListFolderAsync(folder.PhotoId, cancellationToken))
        {
            var isKept = keep is not null && (file.Key == keep || file.Key == keep + ".tmp");
            if (!isKept && file.LastWriteUtc < now - LeftoverFileAge)
            {
                await _storage.DeleteFileAsync(file.Key, cancellationToken);
                removed++;
            }
        }

        return removed;
    }

    private static async Task<T?> TryAsync<T>(Func<Task<T>> action)
        where T : struct
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or RepositoryBusyException)
        {
            return null;
        }
    }
}
