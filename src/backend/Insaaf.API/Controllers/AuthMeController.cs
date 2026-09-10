using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Auth;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

[ApiController]
[Route("api/v1/auth/me")]
public sealed class AuthMeController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthMeController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Get(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return this.FailEnvelope<UserDto>(
                "unauthorized",
                "User is not authorized.",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (AuthException ex)
        {
            return this.FailEnvelope<UserDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
