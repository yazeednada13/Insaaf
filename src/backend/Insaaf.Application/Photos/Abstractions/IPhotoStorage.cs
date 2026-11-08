namespace Insaaf.Application.Photos.Abstractions;

/// <summary>A file in a photo's folder, for the cleanup sweep.</summary>
public sealed record PhotoFileEntry(string Key, DateTime LastWriteUtc);

/// <summary>A photo folder, for the cleanup sweep.</summary>
public sealed record PhotoFolderEntry(Guid PhotoId, DateTime LastWriteUtc);

/// <summary>
/// Where review photo files live (Phase 9A plan §7). Each photo has its own folder, and each processing
/// attempt writes its own file in it, named by the attempt's lease. Nothing is ever overwritten; the
/// database decides which file is served.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Writes the attempt's file (temp file, then a rename that never overwrites). Returns its key.</summary>
    Task<string> SaveAttemptAsync(Guid photoId, Guid leaseId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>The file for a key, or null when it doesn't exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PhotoFileEntry>> ListFolderAsync(Guid photoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PhotoFolderEntry>> ListFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes one file. Idempotent: a missing file counts as deleted.</summary>
    Task DeleteFileAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes the photo's whole folder. Idempotent; returns only once it's confirmed gone.</summary>
    Task DeleteFolderAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>The key a given attempt writes, so callers can recognise files.</summary>
    string KeyFor(Guid photoId, Guid leaseId);
}
