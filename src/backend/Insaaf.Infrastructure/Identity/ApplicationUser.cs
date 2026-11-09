using Insaaf.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Insaaf.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public string? Bio { get; set; }

    /// <summary>
    /// Set when an Admin suspends the account (Phase 9). Separate from Identity's LockoutEnd, so lifting a
    /// suspension never clears a failed-login lockout.
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>The Admin who suspended the account. No foreign key (ADR-017).</summary>
    public Guid? SuspendedByUserId { get; set; }

    /// <summary>The stored profile photo's key (Phase 11); null shows initials.</summary>
    public string? AvatarKey { get; set; }

    /// <summary>Taste Setup step 2 (Phase 11); stored only.</summary>
    public TastePriority TastePriorities { get; set; }

    /// <summary>When the user finished Taste Setup (Phase 11); null means the app offers it.</summary>
    public DateTime? TasteSetupCompletedAt { get; set; }

    /// <summary>In-app notification settings (Phase 11). True by default.</summary>
    public bool NotifyLikes { get; set; } = true;

    public bool NotifyComments { get; set; } = true;

    public bool NotifyFollows { get; set; } = true;
}
