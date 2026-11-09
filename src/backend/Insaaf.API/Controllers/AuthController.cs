using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.Application.Auth;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingExtensions.AuthRegisterPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (AuthException ex)
        {
            return this.FailEnvelope<AuthResponseDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LogoutResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LogoutResultDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LogoutResultDto>>> Logout(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return this.FailEnvelope<LogoutResultDto>(
                "unauthorized",
                "User is not authorized.",
                StatusCodes.Status401Unauthorized);
        }

        await _authService.LogoutAsync(userId, cancellationToken);
        return this.OkEnvelope(new LogoutResultDto(true));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.AuthLoginPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (AuthException ex)
        {
            return this.FailEnvelope<AuthResponseDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingExtensions.AuthRefreshPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RefreshAsync(request, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (AuthException ex)
        {
            return this.FailEnvelope<AuthResponseDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
