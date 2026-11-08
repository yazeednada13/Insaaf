using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Photos;
using Insaaf.Application.Photos.Dtos;
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
    // A little above the 5 MB photo limit, so the service can answer file_too_large itself.
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    private readonly IVerificationService _verificationService;
    private readonly IReviewPhotoService _photoService;

    public VerificationController(IVerificationService verificationService, IReviewPhotoService photoService)
    {
        _verificationService = verificationService;
        _photoService = photoService;
    }

    /// <summary>
    /// Uploads one photo for a visit token (Phase 9A). Multipart fields: token, clientUploadId, file.
    /// 201 for a new photo, 200 when the same clientUploadId was already uploaded. Never redeems the token.
    /// </summary>
    [HttpPost("photos")]
    [EnableRateLimiting(RateLimitingExtensions.VerificationPhotoPolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<ApiResponse<UploadedPhotoDto>>> UploadPhoto(
        [FromForm] string? token,
        [FromForm] Guid? clientUploadId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return this.FailEnvelope<UploadedPhotoDto>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);
        }

        if (file is not null && file.Length > ReviewPhotoService.MaxBytes)
        {
            return this.FailEnvelope<UploadedPhotoDto>(
                "file_too_large",
                "A photo must be at most 5 MB.",
                StatusCodes.Status413PayloadTooLarge);
        }

        byte[] content = [];
        if (file is not null)
        {
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        try
        {
            var result = await _photoService.UploadAsync(userId, token, clientUploadId, content, cancellationToken);
            return result.Created ? this.CreatedEnvelope(result.Photo) : this.OkEnvelope(result.Photo);
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<UploadedPhotoDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    /// <summary>The uploader removes their own photo before it's attached (frees its slot).</summary>
    [HttpDelete("photos/{id:guid}")]
    [EnableRateLimiting(RateLimitingExtensions.VerificationPhotoPolicy)]
    public async Task<ActionResult<ApiResponse<PhotoRemovedDto>>> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return this.FailEnvelope<PhotoRemovedDto>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);
        }

        try
        {
            return this.OkEnvelope(await _photoService.DeleteOwnAsync(userId, id, cancellationToken));
        }
        catch (VerificationException ex)
        {
            return this.FailEnvelope<PhotoRemovedDto>(ex.Code, ex.Message, ex.StatusCode);
        }
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
