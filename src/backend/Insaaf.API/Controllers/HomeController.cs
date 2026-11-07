using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

/// <summary>The الرئيسية tab's restaurant sections.</summary>
[ApiController]
[Authorize]
[Route("api/v1/home")]
public sealed class HomeController : ControllerBase
{
    private readonly IHomeService _homeService;

    public HomeController(IHomeService homeService)
    {
        _homeService = homeService;
    }

    [HttpGet("suggested")]
    [ProducesResponseType(typeof(ApiResponse<HomeRestaurantsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HomeRestaurantsDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HomeRestaurantsDto>>> Suggested(
        [FromQuery] HomeRestaurantsQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return this.FailEnvelope<HomeRestaurantsDto>(
                "unauthorized",
                "Authentication is required.",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await _homeService.GetSuggestedAsync(userId, query, cancellationToken));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<HomeRestaurantsDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("trending")]
    [ProducesResponseType(typeof(ApiResponse<HomeRestaurantsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HomeRestaurantsDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HomeRestaurantsDto>>> Trending(
        [FromQuery] HomeRestaurantsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(await _homeService.GetTrendingAsync(query, cancellationToken));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<HomeRestaurantsDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
