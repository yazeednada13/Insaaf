using System.Security.Claims;
using Insaaf.Application.Auth;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.API.Extensions;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("login")]
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
