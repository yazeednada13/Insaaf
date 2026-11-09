namespace Insaaf.Domain.Enums;

public enum NotificationType
{
    Like = 1,
    Comment = 2,
    Follow = 3,

    /// <summary>The restaurant officially replied to the recipient's review (Phase 11). Written by an Admin.</summary>
    Reply = 4,
}
