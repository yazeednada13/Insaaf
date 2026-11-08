namespace Insaaf.Domain.Enums;

/// <summary>Lifecycle of a review photo (Phase 9A). Only <see cref="Ready"/> photos are ever served.</summary>
public enum ReviewPhotoStatus
{
    /// <summary>The row exists and a processing attempt may be writing its file.</summary>
    Pending = 0,

    /// <summary>Processed and stored; <c>StorageKey</c> names the served file.</summary>
    Ready = 1,

    /// <summary>Removed from view; its folder is being deleted.</summary>
    Deleting = 2,

    /// <summary>Its folder was confirmed empty. Kept as a tombstone that still tracks the folder.</summary>
    Purged = 3,
}
