using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Photos;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Reviews;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/restaurants")]
public sealed class RestaurantsController : ControllerBase
{
    private readonly IRestaurantService _restaurantService;
    private readonly IReviewService _reviewService;
    private readonly ISavedService _savedService;
    private readonly IRestaurantPhotoService _photoService;

    public RestaurantsController(
        IRestaurantService restaurantService,
        IReviewService reviewService,
        ISavedService savedService,
        IRestaurantPhotoService photoService)
    {
        _restaurantService = restaurantService;
        _reviewService = reviewService;
        _savedService = savedService;
        _photoService = photoService;
    }

    /// <summary>«صور المجتمع» (Phase 11): photos of the restaurant's public reviews, newest first.</summary>
    [HttpGet("{id:guid}/photos")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantPhotoListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantPhotoListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantPhotoListResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RestaurantPhotoListResultDto>>> Photos(
        Guid id,
        [FromQuery] RestaurantPhotosQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(await _photoService.ListAsync(id, query, cancellationToken));
        }
        catch (RestaurantException ex)
        {
            return this.FailEnvelope<RestaurantPhotoListResultDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    // Phase 11: saving a restaurant (private to the caller).

    [HttpPut("{id:guid}/save")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<SavedStateDto>>> Save(Guid id, CancellationToken cancellationToken) =>
        RunSocial(userId => _savedService.SaveRestaurantAsync(userId, id, cancellationToken));

    [HttpDelete("{id:guid}/save")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<SavedStateDto>>> Unsave(Guid id, CancellationToken cancellationToken) =>
        RunSocial(userId => _savedService.UnsaveRestaurantAsync(userId, id, cancellationToken));

    private async Task<ActionResult<ApiResponse<T>>> RunSocial<T>(Func<Guid, Task<T>> action)
    {
        if (!User.TryGetUserId(out var userId))
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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<RestaurantListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantListResultDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<RestaurantListResultDto>>> Search(
        [FromQuery] RestaurantSearchQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _restaurantService.SearchAsync(query, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (RestaurantException ex)
        {
            return this.FailEnvelope<RestaurantListResultDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDetailDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RestaurantDetailDto>>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var restaurantId))
        {
            return this.FailEnvelope<RestaurantDetailDto>(
                "not_found",
                "Restaurant was not found.",
                StatusCodes.Status404NotFound);
        }

        try
        {
            var result = await _restaurantService.GetByIdAsync(restaurantId, cancellationToken);
            // Phase 11: the caller's own saved state.
            if (User.TryGetUserId(out var userId))
            {
                result = result with { IsSavedByMe = await _savedService.IsRestaurantSavedAsync(userId, restaurantId, cancellationToken) };
            }

            return this.OkEnvelope(result);
        }
        catch (RestaurantException ex)
        {
            return this.FailEnvelope<RestaurantDetailDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    // Read-only (Phase 4). Review creation is only through the verified QR flow (Phase 5).
    [HttpGet("{id}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReviewListResultDto>>> GetReviews(
        string id,
        [FromQuery] RestaurantReviewsQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var restaurantId))
        {
            return this.FailEnvelope<ReviewListResultDto>(
                "not_found",
                "Restaurant was not found.",
                StatusCodes.Status404NotFound);
        }

        // [Authorize] guarantees the claim; the viewer only decides isLikedByMe.
        Guid.TryParse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var viewerUserId);

        try
        {
            var result = await _reviewService.ListForRestaurantAsync(restaurantId, query, viewerUserId, cancellationToken);
            return this.OkEnvelope(result);
        }
        catch (RestaurantException ex)
        {
            return this.FailEnvelope<ReviewListResultDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
