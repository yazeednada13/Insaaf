using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Controllers;

/// <summary>Following users. The follower always comes from the JWT, never from the request.</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/{id:guid}/follow")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserFollowService _followService;

    public UsersController(IUserFollowService followService)
    {
        _followService = followService;
    }

    [HttpPut]
    [EnableRateLimiting(RateLimitingExtensions.SocialFollowPolicy)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<UserFollowStateDto>>> Follow(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _followService.FollowAsync(userId, id, cancellationToken));

    [HttpDelete]
    [EnableRateLimiting(RateLimitingExtensions.SocialFollowPolicy)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<UserFollowStateDto>>> Unfollow(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _followService.UnfollowAsync(userId, id, cancellationToken));

    private async Task<ActionResult<ApiResponse<UserFollowStateDto>>> Run(Func<Guid, Task<UserFollowStateDto>> action)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return this.FailEnvelope<UserFollowStateDto>(
                "unauthorized",
                "Authentication is required.",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await action(userId));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<UserFollowStateDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
