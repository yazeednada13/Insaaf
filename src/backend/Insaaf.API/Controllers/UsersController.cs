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

/// <summary>
/// Profiles, people search and following. The caller always comes from the JWT, never from the
/// request, and these responses never contain email or roles.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserFollowService _followService;
    private readonly IUserProfileService _profileService;
    private readonly IUserTasteService _tasteService;

    public UsersController(
        IUserFollowService followService,
        IUserProfileService profileService,
        IUserTasteService tasteService)
    {
        _followService = followService;
        _profileService = profileService;
        _tasteService = tasteService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<UserProfileDto>>> GetMe(CancellationToken cancellationToken) =>
        Run(userId => _profileService.GetMineAsync(userId, cancellationToken));

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status409Conflict)]
    public Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        Run(userId => _profileService.UpdateMineAsync(userId, request, cancellationToken));

    [HttpGet("search")]
    [EnableRateLimiting(RateLimitingExtensions.SocialSearchPolicy)]
    [ProducesResponseType(typeof(ApiResponse<UserSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserSearchResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserSearchResultDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<UserSearchResultDto>>> Search(
        [FromQuery] UserSearchQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _profileService.SearchAsync(userId, query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<UserProfileDto>>> Get(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _profileService.GetAsync(id, userId, cancellationToken));

    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<FeedResultDto>>> GetReviews(
        Guid id,
        [FromQuery] FeedQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _profileService.ListReviewsAsync(id, userId, query, cancellationToken));

    [HttpGet("{id:guid}/taste")]
    [ProducesResponseType(typeof(ApiResponse<TasteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TasteDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<TasteDto>>> GetTaste(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _tasteService.GetTasteAsync(id, userId, cancellationToken));

    [HttpGet("{id:guid}/taste-match")]
    [ProducesResponseType(typeof(ApiResponse<TasteMatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TasteMatchDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<TasteMatchDto>>> GetTasteMatch(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _tasteService.GetMatchAsync(id, userId, cancellationToken));

    [HttpGet("{id:guid}/followers")]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<UserListResultDto>>> GetFollowers(
        Guid id,
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _profileService.ListFollowersAsync(id, userId, query, cancellationToken));

    [HttpGet("{id:guid}/following")]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserListResultDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<UserListResultDto>>> GetFollowing(
        Guid id,
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _profileService.ListFollowingAsync(id, userId, query, cancellationToken));

    [HttpPut("{id:guid}/follow")]
    [EnableRateLimiting(RateLimitingExtensions.SocialFollowPolicy)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<UserFollowStateDto>>> Follow(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _followService.FollowAsync(userId, id, cancellationToken));

    [HttpDelete("{id:guid}/follow")]
    [EnableRateLimiting(RateLimitingExtensions.SocialFollowPolicy)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserFollowStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<UserFollowStateDto>>> Unfollow(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _followService.UnfollowAsync(userId, id, cancellationToken));

    private async Task<ActionResult<ApiResponse<T>>> Run<T>(Func<Guid, Task<T>> action)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return this.FailEnvelope<T>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await action(userId));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<T>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
