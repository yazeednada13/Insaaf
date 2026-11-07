using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Entities;

/// <summary>
/// An in-app notification: someone liked or commented on the recipient's review, or followed them.
/// The text is built on the device from <see cref="Type"/> and the actor; the server stores none.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; set; }

    /// <summary>The Identity user who receives it. The relationship is configured in Infrastructure.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>The Identity user who acted.</summary>
    public Guid ActorId { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>The review that was liked or commented on; null for a follow.</summary>
    public Guid? ReviewId { get; set; }

    /// <summary>The comment, for a comment notification; deleted with it by the comment service.</summary>
    public Guid? CommentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
