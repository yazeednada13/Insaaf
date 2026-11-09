namespace Insaaf.Application.Photos.Abstractions;

/// <summary>
/// Where profile photos live (Phase 11). Kept apart from review photos: their folder layout and cleanup
/// sweep are built around review-photo rows. A key is a fresh id per upload, so a file is never rewritten.
/// </summary>
public interface IAvatarStorage
{
    /// <summary>Writes a new file and returns its key.</summary>
    Task<string> SaveAsync(ReadOnlyMemory<byte> jpeg, CancellationToken cancellationToken = default);

    /// <summary>The file for a key, or null when it doesn't exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Idempotent: a missing file counts as deleted.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
