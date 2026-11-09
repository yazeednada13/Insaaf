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
    private readonly ISavedService _savedService;
    private readonly IUserAvatarService _avatarService;
    private readonly ITasteSetupService _tasteSetupService;
    private readonly IUserSettingsService _settingsService;

    private const long MaxAvatarRequestBytes = 6 * 1024 * 1024;

    public UsersController(
        IUserFollowService followService,
        IUserProfileService profileService,
        IUserTasteService tasteService,
        ISavedService savedService,
        IUserAvatarService avatarService,
        ITasteSetupService tasteSetupService,
        IUserSettingsService settingsService)
    {
        _settingsService = settingsService;
        _avatarService = avatarService;
        _tasteSetupService = tasteSetupService;
        _followService = followService;
        _profileService = profileService;
        _tasteService = tasteService;
        _savedService = savedService;
    }

    // Phase 11: settings (in-app notifications).

    [HttpGet("me/settings")]
    [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<NotificationSettingsDto>>> GetSettings(CancellationToken cancellationToken) =>
        Run(userId => _settingsService.GetAsync(userId, cancellationToken));

    [HttpPut("me/settings")]
    [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<NotificationSettingsDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<NotificationSettingsDto>>> SetSettings(
        [FromBody] NotificationSettingsDto? request,
        CancellationToken cancellationToken) =>
        Run(userId => _settingsService.SetAsync(userId, request, cancellationToken));

    // Phase 11: Taste Setup.

    [HttpGet("me/taste-preferences")]
    [ProducesResponseType(typeof(ApiResponse<TastePreferencesDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<TastePreferencesDto>>> GetTastePreferences(CancellationToken cancellationToken) =>
        Run(userId => _tasteSetupService.GetAsync(userId, cancellationToken));

    [HttpPut("me/taste-preferences")]
    [ProducesResponseType(typeof(ApiResponse<TastePreferencesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TastePreferencesDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<TastePreferencesDto>>> SetTastePreferences(
        [FromBody] SetTastePreferencesRequest? request,
        CancellationToken cancellationToken) =>
        Run(userId => _tasteSetupService.SaveAsync(userId, request, cancellationToken));

    [HttpGet("suggested")]
    [ProducesResponseType(typeof(ApiResponse<SuggestedUsersDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SuggestedUsersDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<SuggestedUsersDto>>> GetSuggested(
        [FromQuery] SuggestedUsersQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _tasteSetupService.SuggestUsersAsync(userId, query, cancellationToken));

    // Phase 11: the caller's profile photo, and serving anyone's.

    [HttpPut("me/avatar")]
    [EnableRateLimiting(RateLimitingExtensions.VerificationPhotoPolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAvatarRequestBytes)]
    [ProducesResponseType(typeof(ApiResponse<UserAvatarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserAvatarDto>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiResponse<UserAvatarDto>), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ApiResponse<UserAvatarDto>>> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is not null && file.Length > UserAvatarService.MaxBytes)
        {
            return this.FailEnvelope<UserAvatarDto>("file_too_large", "A photo must be at most 5 MB.", StatusCodes.Status413PayloadTooLarge);
        }

        byte[] content = [];
        if (file is not null)
        {
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        return await Run(userId => _avatarService.UploadAsync(userId, content, cancellationToken));
    }

    [HttpDelete("me/avatar")]
    [ProducesResponseType(typeof(ApiResponse<UserAvatarDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<UserAvatarDto>>> RemoveAvatar(CancellationToken cancellationToken) =>
        Run(userId => _avatarService.RemoveAsync(userId, cancellationToken));

    /// <summary>
    /// A profile photo. Anonymous for the same reason as review photos (React Native's Image doesn't send the
    /// token); Admin accounts never have one. The URL carries the key, so a new photo is a new URL.
    /// </summary>
    [HttpGet("{id:guid}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(Guid id, CancellationToken cancellationToken)
    {
        var photo = await _avatarService.OpenAsync(id, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "private, no-cache";
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ETag] = photo.ETag;
        if (Request.Headers.IfNoneMatch.Any(value => value == photo.ETag))
        {
            await photo.Content.DisposeAsync();
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(photo.Content, "image/jpeg");
    }

    // Phase 11: the caller's own saved items. Only "me": nobody can read another user's saved lists.

    [HttpGet("me/saved/restaurants")]
    [ProducesResponseType(typeof(ApiResponse<SavedRestaurantsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SavedRestaurantsResultDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<SavedRestaurantsResultDto>>> GetSavedRestaurants(
        [FromQuery] SavedRestaurantsQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _savedService.ListRestaurantsAsync(userId, query, cancellationToken));

    [HttpGet("me/saved/reviews")]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FeedResultDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<FeedResultDto>>> GetSavedReviews(
        [FromQuery] FeedQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _savedService.ListReviewsAsync(userId, query, cancellationToken));

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
