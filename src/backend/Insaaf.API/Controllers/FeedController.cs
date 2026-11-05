using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

/// <summary>The following feed: verified public reviews by users the caller follows.</summary>
[ApiController]
[Authorize]
[Route("api/v1/feed")]
public sealed class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<FeedResultDto>>> Get(
        [FromQuery] FeedQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return this.FailEnvelope<FeedResultDto>(
                "unauthorized",
                "Authentication is required.",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await _feedService.GetAsync(userId, query, cancellationToken));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<FeedResultDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
