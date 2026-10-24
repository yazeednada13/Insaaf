using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Restaurants.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/restaurants")]
public sealed class RestaurantsController : ControllerBase
{
    private readonly IRestaurantService _restaurantService;

    public RestaurantsController(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
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
            return this.OkEnvelope(result);
        }
        catch (RestaurantException ex)
        {
            return this.FailEnvelope<RestaurantDetailDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
