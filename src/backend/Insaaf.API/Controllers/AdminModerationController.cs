using Insaaf.API.Extensions;
using Insaaf.Application.Admin;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

/// <summary>
/// Review, comment and account moderation (Phase 9). The Admin role is enforced here only; the acting
/// Admin's id comes from the JWT and is passed to the services as <c>actorId</c>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/admin")]
public sealed class AdminModerationController : ControllerBase
{
    private readonly IReviewModerationService _reviews;
    private readonly IUserModerationService _users;

    public AdminModerationController(IReviewModerationService reviews, IUserModerationService users)
    {
        _reviews = reviews;
        _users = users;
    }

    [HttpGet("reviews")]
    public Task<ActionResult<ApiResponse<AdminReviewListResultDto>>> ListReviews(
        [FromQuery] Guid? restaurantId,
        [FromQuery] Guid? userId,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Run(_ => _reviews.ListAsync(new AdminReviewListQuery(restaurantId, userId, cursor, limit), cancellationToken));

    [HttpPut("reviews/{id:guid}/visibility")]
    public Task<ActionResult<ApiResponse<AdminReviewDto>>> SetReviewVisibility(
        Guid id,
        [FromBody] SetReviewVisibilityRequest? request,
        CancellationToken cancellationToken) =>
        Run(actorId => _reviews.SetVisibilityAsync(actorId, id, request, cancellationToken));

    [HttpDelete("comments/{id:guid}")]
    public Task<ActionResult<ApiResponse<AdminCommentDeletedDto>>> DeleteComment(Guid id, CancellationToken cancellationToken) =>
        Run(actorId => _reviews.DeleteCommentAsync(actorId, id, cancellationToken));

    [HttpGet("users")]
    public Task<ActionResult<ApiResponse<AdminUserSearchResultDto>>> SearchUsers(
        [FromQuery] string? query,
        CancellationToken cancellationToken) =>
        Run(_ => _users.SearchAsync(query, cancellationToken));

    [HttpPut("users/{id:guid}/suspension")]
    public Task<ActionResult<ApiResponse<AdminUserDto>>> SetSuspension(
        Guid id,
        [FromBody] SetSuspensionRequest? request,
        CancellationToken cancellationToken) =>
        Run(actorId => _users.SetSuspensionAsync(actorId, id, request, cancellationToken));

    private async Task<ActionResult<ApiResponse<T>>> Run<T>(Func<Guid, Task<T>> action)
    {
        if (!User.TryGetUserId(out var actorId))
        {
            return this.FailEnvelope<T>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await action(actorId));
        }
        catch (AdminException ex)
        {
            return this.FailEnvelope<T>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
