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
}
