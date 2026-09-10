using Microsoft.AspNetCore.Identity;

namespace Insaaf.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
}
