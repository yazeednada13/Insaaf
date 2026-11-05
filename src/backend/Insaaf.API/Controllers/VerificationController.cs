using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Verification;
using Insaaf.Application.Verification.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Controllers;

/// <summary>
/// The only review-creation path in the MVP (ADR-004): a scanned visit token resolves the restaurant on
/// the server, and submitting redeems the token and creates the verified review atomically.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/verification")]
public sealed class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;

    public VerificationController(IVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpPost("visits/resolve")]
    [EnableRateLimiting(RateLimitingExtensions.VerificationResolvePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedVisitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedVisitDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedVisitDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedVisitDto>), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ApiResponse<ResolvedVisitDto>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ResolvedVisitDto>>> Resolve(
        [FromBody] ResolveVisitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return this.OkEnvelope(await _verificationService.ResolveAsync(request, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<ResolvedVisitDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpPost("reviews")]
    [EnableRateLimiting(RateLimitingExtensions.VerificationCreatePolicy)]
    [ProducesResponseType(typeof(ApiResponse<VerifiedReviewCreatedDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<VerifiedReviewCreatedDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<VerifiedReviewCreatedDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<VerifiedReviewCreatedDto>), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ApiResponse<VerifiedReviewCreatedDto>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<VerifiedReviewCreatedDto>>> CreateReview(
        [FromBody] CreateVerifiedReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return this.FailEnvelope<VerifiedReviewCreatedDto>(
                "unauthorized",
                "Authentication is required.",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.CreatedEnvelope(await _verificationService.CreateReviewAsync(userId, request, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<VerifiedReviewCreatedDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
