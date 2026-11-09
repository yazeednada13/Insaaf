using Insaaf.Application.Photos.Abstractions;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Photos;

/// <summary>
/// Local-disk profile photos (Phase 11): <c>{photo root}/avatars/{key}.jpg</c>. The review-photo sweep only
/// looks at folders named by a photo id, so it never touches this folder. Keys are parsed back into ids
/// before any file access, so a key can't point outside the folder. Phase 12 moves this to cloud storage.
/// </summary>
public sealed class LocalAvatarStorage : IAvatarStorage
{
    private const string FolderName = "avatars";
    private const string Extension = ".jpg";

    private readonly string _folder;

    public LocalAvatarStorage(IOptions<PhotoStorageOptions> options)
    {
        _folder = Path.Combine(options.Value.ResolveRoot(), FolderName);
    }

    public async Task<string> SaveAsync(ReadOnlyMemory<byte> jpeg, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);
        var key = Guid.NewGuid().ToString("N");
        var finalPath = Path.Combine(_folder, key + Extension);
        var tempPath = finalPath + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await stream.WriteAsync(jpeg, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, finalPath, overwrite: false);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string? PathFor(string key) =>
        Guid.TryParseExact(key, "N", out var id) ? Path.Combine(_folder, id.ToString("N") + Extension) : null;
}
