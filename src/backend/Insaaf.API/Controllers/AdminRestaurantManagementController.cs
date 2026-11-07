using Insaaf.API.Extensions;
using Insaaf.Application.Admin;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

/// <summary>
/// Restaurant catalog management (Phase 9, task 9.1). The Admin role is enforced here only; the acting
/// Admin's id comes from the JWT and is passed to the service as <c>actorId</c>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/admin/restaurants")]
public sealed class AdminRestaurantManagementController : ControllerBase
{
    private readonly IRestaurantAdminService _service;

    public AdminRestaurantManagementController(IRestaurantAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<ActionResult<ApiResponse<AdminRestaurantListResultDto>>> List(
        [FromQuery] bool includeInactive = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Run(_ => _service.ListAsync(new AdminRestaurantListQuery(includeInactive, page, pageSize), cancellationToken));

    [HttpPost]
    public Task<ActionResult<ApiResponse<AdminRestaurantDto>>> Create(
        [FromBody] UpsertRestaurantRequest? request,
        CancellationToken cancellationToken) =>
        Run(actorId => _service.CreateAsync(actorId, request, cancellationToken), created: true);

    [HttpPut("{id:guid}")]
    public Task<ActionResult<ApiResponse<AdminRestaurantDto>>> Update(
        Guid id,
        [FromBody] UpsertRestaurantRequest? request,
        CancellationToken cancellationToken) =>
        Run(actorId => _service.UpdateAsync(actorId, id, request, cancellationToken));

    [HttpPut("{id:guid}/status")]
    public Task<ActionResult<ApiResponse<AdminRestaurantDto>>> SetStatus(
        Guid id,
        [FromBody] SetRestaurantStatusRequest? request,
        CancellationToken cancellationToken) =>
        Run(actorId => _service.SetStatusAsync(actorId, id, request, cancellationToken));

    private async Task<ActionResult<ApiResponse<T>>> Run<T>(Func<Guid, Task<T>> action, bool created = false)
    {
        if (!User.TryGetUserId(out var actorId))
        {
            return this.FailEnvelope<T>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await action(actorId);
            return created ? this.CreatedEnvelope(result) : this.OkEnvelope(result);
        }
        catch (AdminException ex)
        {
            return this.FailEnvelope<T>(ex.Code, ex.Message, ex.StatusCode);
        }
    }
}
