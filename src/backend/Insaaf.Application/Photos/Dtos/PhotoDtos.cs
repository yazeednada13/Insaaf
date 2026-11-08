namespace Insaaf.Application.Photos.Dtos;

public sealed record UploadedPhotoDto(Guid Id, int Width, int Height);

/// <summary><see cref="Created"/> is false when an earlier upload of the same logical photo is returned.</summary>
public sealed record PhotoUploadResult(UploadedPhotoDto Photo, bool Created);

public static class FileDeletionState
{
    /// <summary>The photo's folder was confirmed empty in this request.</summary>
    public const string Completed = "completed";

    /// <summary>The photo is no longer visible, but its files are still being deleted by cleanup.</summary>
    public const string Pending = "pending";
}

/// <summary><see cref="Removed"/> means the photo is no longer visible; <see cref="FileDeletion"/> reports the files honestly.</summary>
public sealed record PhotoRemovedDto(Guid PhotoId, bool Removed, string FileDeletion);

/// <summary>A photo as shown on a review: its URL is a relative API path.</summary>
public sealed record ReviewPhotoDto(Guid Id, string Url, int Width, int Height)
{
    public static ReviewPhotoDto From(Guid id, int width, int height) =>
        new(id, $"/api/v1/review-photos/{id}", width, height);
}

/// <summary>An image ready to send: the stream and its entity tag.</summary>
public sealed record ServablePhoto(Stream Content, string ETag) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>What one cleanup run did; counts only.</summary>
public sealed record PhotoCleanupReport(
    int Tombstoned,
    int Purged,
    int FilesSwept,
    int FoldersSwept,
    int Pruned,
    int CountersRepaired,
    int Failures);
