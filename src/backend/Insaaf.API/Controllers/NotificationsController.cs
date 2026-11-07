using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

/// <summary>The caller's in-app notifications. The recipient always comes from the JWT.</summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<NotificationListResultDto>), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ApiResponse<NotificationListResultDto>>> List(
        [FromQuery] NotificationsQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _notificationService.ListAsync(userId, query, cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<UnreadCountDto>>> UnreadCount(CancellationToken cancellationToken) =>
        Run(userId => _notificationService.CountUnreadAsync(userId, cancellationToken));

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<UnreadCountDto>>> ReadAll(CancellationToken cancellationToken) =>
        Run(userId => _notificationService.MarkAllReadAsync(userId, cancellationToken));

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
