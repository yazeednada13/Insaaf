using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Admin;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Verification;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

public sealed record IssueVisitTokensRequest(int Count, int? ExpiresInDays);

public sealed record RevokeVisitTokensRequest(Guid? IssuanceId);

/// <summary>
/// Admin operations for partner restaurants (Phase 5 manual channel). The Admin role is enforced only
/// here; the services below are channel-independent and receive an <see cref="IssuancePrincipal"/>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/admin/restaurants/{id:guid}")]
public sealed class AdminRestaurantsController : ControllerBase
{
    private const int DefaultExpiresInDays = 90;
    private const int MaxExpiresInDays = 365;

    private readonly IRestaurantPartnershipService _partnershipService;
    private readonly IVisitTokenIssuer _issuer;
    private readonly IVisitTokenLifecycleService _lifecycleService;
    private readonly IDateTimeProvider _clock;

    public AdminRestaurantsController(
        IRestaurantPartnershipService partnershipService,
        IVisitTokenIssuer issuer,
        IVisitTokenLifecycleService lifecycleService,
        IDateTimeProvider clock)
    {
        _partnershipService = partnershipService;
        _issuer = issuer;
        _lifecycleService = lifecycleService;
        _clock = clock;
    }

    [HttpPut("partnership")]
    public async Task<ActionResult<ApiResponse<RestaurantPartnershipDto>>> SetPartnership(
        Guid id,
        [FromBody] SetPartnershipRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(await _partnershipService.SetAsync(id, request, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<RestaurantPartnershipDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    /// <remarks>Payloads contain the secret tokens and are returned only in this response.</remarks>
    [HttpPost("visit-tokens")]
    public async Task<ActionResult<ApiResponse<IssuedVisitTokensDto>>> IssueVisitTokens(
        Guid id,
        [FromBody] IssueVisitTokensRequest request,
        CancellationToken cancellationToken)
    {
        var expiresInDays = request.ExpiresInDays ?? DefaultExpiresInDays;
        if (expiresInDays is < 1 or > MaxExpiresInDays)
        {
            return this.FailEnvelope<IssuedVisitTokensDto>(
                "validation_error",
                $"Expires in days must be between 1 and {MaxExpiresInDays}.",
                StatusCodes.Status400BadRequest);
        }

        try
        {
            var issued = await _issuer.IssueAsync(
                AdminPrincipal(),
                new VisitTokenIssueRequest(id, request.Count, _clock.UtcNow.AddDays(expiresInDays)),
                cancellationToken);
            return this.CreatedEnvelope(issued);
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<IssuedVisitTokensDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("visit-tokens/revoke")]
    public async Task<ActionResult<ApiResponse<RevokedVisitTokensDto>>> RevokeVisitTokens(
        Guid id,
        [FromBody] RevokeVisitTokensRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(
                await _lifecycleService.RevokeUnusedAsync(AdminPrincipal(), id, request.IssuanceId, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<RevokedVisitTokensDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpGet("visit-tokens/summary")]
    public async Task<ActionResult<ApiResponse<VisitTokenSummaryDto>>> GetVisitTokenSummary(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(await _lifecycleService.GetSummaryAsync(AdminPrincipal(), id, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<VisitTokenSummaryDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    // The principal is built from the authenticated identity, never from the request body.
    private IssuancePrincipal AdminPrincipal() =>
        IssuancePrincipal.ForAdmin(Guid.Parse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!));
}
