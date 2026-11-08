using Insaaf.Application.Photos.Abstractions;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Photos;

/// <summary>
/// Local-disk photo storage for development and the pilot (D-1). Layout: <c>{root}/{photoId}/{leaseId}.jpg</c>.
/// Keys are relative (<c>{photoId}/{leaseId}.jpg</c>) and are parsed back into GUIDs before any file
/// access, so a key can never point outside the root. Phase 11 replaces this class with cloud storage.
/// </summary>
public sealed class LocalPhotoStorage : IPhotoStorage
{
    private const string Extension = ".jpg";
    private const string TempSuffix = ".tmp";

    private readonly string _root;

    public LocalPhotoStorage(IOptions<PhotoStorageOptions> options)
    {
        _root = options.Value.ResolveRoot();
    }

    public string KeyFor(Guid photoId, Guid leaseId) => $"{photoId:N}/{leaseId:N}{Extension}";

    public async Task<string> SaveAttemptAsync(
        Guid photoId,
        Guid leaseId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var finalPath = PathFor(photoId, leaseId);
        var tempPath = finalPath + TempSuffix;
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        // The name belongs to this attempt only, so the rename never replaces another attempt's file.
        File.Move(tempPath, finalPath, overwrite: false);
        return KeyFor(photoId, leaseId);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = FilePathForKey(key, allowTemp: false);
        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<IReadOnlyList<PhotoFileEntry>> ListFolderAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var folder = FolderFor(photoId);
        if (!Directory.Exists(folder))
        {
            return Task.FromResult<IReadOnlyList<PhotoFileEntry>>([]);
        }

        IReadOnlyList<PhotoFileEntry> entries = new DirectoryInfo(folder)
            .EnumerateFiles()
            .Select(file => new PhotoFileEntry($"{photoId:N}/{file.Name}", file.LastWriteTimeUtc))
            .ToList();
        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<PhotoFolderEntry>> ListFoldersAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root))
        {
            return Task.FromResult<IReadOnlyList<PhotoFolderEntry>>([]);
        }

        var folders = new List<PhotoFolderEntry>();
        foreach (var folder in new DirectoryInfo(_root).EnumerateDirectories())
        {
            if (Guid.TryParseExact(folder.Name, "N", out var id))
            {
                folders.Add(new PhotoFolderEntry(id, folder.LastWriteTimeUtc));
            }
        }

        return Task.FromResult<IReadOnlyList<PhotoFolderEntry>>(folders);
    }

    public Task DeleteFileAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = FilePathForKey(key, allowTemp: true);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var folder = FolderFor(photoId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        // Confirmed only when the folder is really gone.
        if (Directory.Exists(folder))
        {
            throw new IOException("The photo folder could not be deleted.");
        }

        return Task.CompletedTask;
    }

    private string FolderFor(Guid photoId) => Path.Combine(_root, photoId.ToString("N"));

    private string PathFor(Guid photoId, Guid leaseId) => Path.Combine(FolderFor(photoId), $"{leaseId:N}{Extension}");

    // Only "{photoId}/{leaseId}.jpg" (and its ".tmp" when allowed) is accepted; anything else is refused.
    private string? FilePathForKey(string key, bool allowTemp)
    {
        var parts = key.Split('/');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var photoId))
        {
            return null;
        }

        var name = parts[1];
        var bare = allowTemp && name.EndsWith(TempSuffix, StringComparison.Ordinal) ? name[..^TempSuffix.Length] : name;
        if (!bare.EndsWith(Extension, StringComparison.Ordinal)
            || !Guid.TryParseExact(bare[..^Extension.Length], "N", out _))
        {
            return null;
        }

        return Path.Combine(FolderFor(photoId), name);
    }
}
