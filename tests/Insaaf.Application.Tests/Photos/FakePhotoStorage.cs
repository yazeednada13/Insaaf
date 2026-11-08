using Insaaf.Application.Photos.Abstractions;

namespace Insaaf.Application.Tests.Photos;

/// <summary>
/// In-memory <see cref="IPhotoStorage"/> for service tests. Failures can be switched on to prove that rows
/// are never purged while their files may still exist.
/// </summary>
public sealed class FakePhotoStorage : IPhotoStorage
{
    public Dictionary<string, (byte[] Content, DateTime Written)> Files { get; } = [];

    public bool FailSaves { get; set; }

    public bool FailDeletes { get; set; }

    /// <summary>Overrides the write time of new files, to simulate old leftovers.</summary>
    public DateTime? WriteTime { get; set; }

    public string KeyFor(Guid photoId, Guid leaseId) => $"{photoId:N}/{leaseId:N}.jpg";

    public Task<string> SaveAttemptAsync(Guid photoId, Guid leaseId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (FailSaves)
        {
            throw new IOException("Simulated storage failure.");
        }

        var key = KeyFor(photoId, leaseId);
        if (Files.ContainsKey(key))
        {
            throw new IOException("An attempt never overwrites a file.");
        }

        Files[key] = (content.ToArray(), WriteTime ?? DateTime.UtcNow);
        return Task.FromResult(key);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var file) ? new MemoryStream(file.Content) : null);

    public Task<IReadOnlyList<PhotoFileEntry>> ListFolderAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PhotoFileEntry>>(Files
            .Where(file => file.Key.StartsWith($"{photoId:N}/", StringComparison.Ordinal))
            .Select(file => new PhotoFileEntry(file.Key, file.Value.Written))
            .ToList());

    public Task<IReadOnlyList<PhotoFolderEntry>> ListFoldersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PhotoFolderEntry>>(Files
            .GroupBy(file => Guid.ParseExact(file.Key[..32], "N"))
            .Select(group => new PhotoFolderEntry(group.Key, group.Max(file => file.Value.Written)))
            .ToList());

    public Task DeleteFileAsync(string key, CancellationToken cancellationToken = default)
    {
        if (FailDeletes)
        {
            throw new IOException("Simulated delete failure.");
        }

        Files.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        if (FailDeletes)
        {
            throw new IOException("Simulated delete failure.");
        }

        foreach (var key in Files.Keys.Where(key => key.StartsWith($"{photoId:N}/", StringComparison.Ordinal)).ToList())
        {
            Files.Remove(key);
        }

        return Task.CompletedTask;
    }

    public bool HasFolder(Guid photoId) => Files.Keys.Any(key => key.StartsWith($"{photoId:N}/", StringComparison.Ordinal));
}

/// <summary>Storage for tests that never touch photos.</summary>
public sealed class NoPhotoStorage : IPhotoStorage
{
    private readonly FakePhotoStorage _inner = new();

    public string KeyFor(Guid photoId, Guid leaseId) => _inner.KeyFor(photoId, leaseId);

    public Task<string> SaveAttemptAsync(Guid photoId, Guid leaseId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) =>
        _inner.SaveAttemptAsync(photoId, leaseId, content, cancellationToken);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => _inner.OpenReadAsync(key, cancellationToken);

    public Task<IReadOnlyList<PhotoFileEntry>> ListFolderAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _inner.ListFolderAsync(photoId, cancellationToken);

    public Task<IReadOnlyList<PhotoFolderEntry>> ListFoldersAsync(CancellationToken cancellationToken = default) => _inner.ListFoldersAsync(cancellationToken);

    public Task DeleteFileAsync(string key, CancellationToken cancellationToken = default) => _inner.DeleteFileAsync(key, cancellationToken);

    public Task DeleteFolderAsync(Guid photoId, CancellationToken cancellationToken = default) => _inner.DeleteFolderAsync(photoId, cancellationToken);
}
