using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Insaaf.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id from the JWT <c>sub</c> claim; never from the request body.</summary>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
