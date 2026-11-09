using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Insaaf.API.Extensions;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Reviews;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Controllers;

/// <summary>
/// Reading one review and its social actions. This controller never creates a review: that happens
/// only through the verified QR flow (ADR-004). Likes and comments never affect ratings.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/reviews/{id:guid}")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IReviewLikeService _likeService;
    private readonly IReviewCommentService _commentService;
    private readonly ISavedService _savedService;
    private readonly IReviewCommentLikeService _commentLikeService;
    private readonly IReviewReportService _reportService;

    public ReviewsController(
        IReviewService reviewService,
        IReviewLikeService likeService,
        IReviewCommentService commentService,
        ISavedService savedService,
        IReviewCommentLikeService commentLikeService,
        IReviewReportService reportService)
    {
        _reportService = reportService;
        _reviewService = reviewService;
        _likeService = likeService;
        _commentService = commentService;
        _savedService = savedService;
        _commentLikeService = commentLikeService;
    }

    // Phase 11: saving (private to the caller) and comment likes.

    [HttpPut("save")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<SavedStateDto>>> Save(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _savedService.SaveReviewAsync(userId, id, cancellationToken));

    [HttpDelete("save")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<SavedStateDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<SavedStateDto>>> Unsave(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _savedService.UnsaveReviewAsync(userId, id, cancellationToken));

    [HttpPost("report")]
    [EnableRateLimiting(RateLimitingExtensions.SocialCommentPolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewReportedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewReportedDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReviewReportedDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewReportedDto>>> Report(
        Guid id,
        [FromBody] ReportReviewRequest? request,
        CancellationToken cancellationToken) =>
        Run(userId => _reportService.ReportAsync(id, userId, request, cancellationToken));

    [HttpPut("comments/{commentId:guid}/like")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentLikeStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentLikeStateDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewCommentLikeStateDto>>> LikeComment(
        Guid id,
        Guid commentId,
        CancellationToken cancellationToken) =>
        Run(userId => _commentLikeService.LikeAsync(id, commentId, userId, cancellationToken));

    [HttpDelete("comments/{commentId:guid}/like")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentLikeStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentLikeStateDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewCommentLikeStateDto>>> UnlikeComment(
        Guid id,
        Guid commentId,
        CancellationToken cancellationToken) =>
        Run(userId => _commentLikeService.UnlikeAsync(id, commentId, userId, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ReviewDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewDetailDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ReviewDetailDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewDetailDto>>> Get(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _reviewService.GetByIdAsync(id, userId, cancellationToken));

    [HttpPut("like")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<ReviewLikeStateDto>>> Like(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _likeService.LikeAsync(id, userId, cancellationToken));

    [HttpDelete("like")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReviewLikeStateDto>), StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ApiResponse<ReviewLikeStateDto>>> Unlike(Guid id, CancellationToken cancellationToken) =>
        Run(userId => _likeService.UnlikeAsync(id, userId, cancellationToken));

    [HttpGet("comments")]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentListResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentListResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentListResultDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewCommentListResultDto>>> GetComments(
        Guid id,
        [FromQuery] ReviewCommentsQuery query,
        CancellationToken cancellationToken) =>
        Run(userId => _commentService.ListAsync(id, userId, query, cancellationToken));

    [HttpPost("comments")]
    [EnableRateLimiting(RateLimitingExtensions.SocialCommentPolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDto>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<ReviewCommentDto>>> CreateComment(
        Guid id,
        [FromBody] CreateReviewCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthenticated<ReviewCommentDto>();
        }

        try
        {
            return this.CreatedEnvelope(await _commentService.CreateAsync(id, userId, request, cancellationToken));
        }
        catch (SocialException ex)
        {
            return this.FailEnvelope<ReviewCommentDto>(ex.Code, ex.Message, ex.StatusCode);
        }
    }

    [HttpDelete("comments/{commentId:guid}")]
    [EnableRateLimiting(RateLimitingExtensions.SocialLikePolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDeletedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDeletedDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCommentDeletedDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<ReviewCommentDeletedDto>>> DeleteComment(
        Guid id,
        Guid commentId,
        CancellationToken cancellationToken) =>
        Run(userId => _commentService.DeleteAsync(id, commentId, userId, cancellationToken));

    private async Task<ActionResult<ApiResponse<T>>> Run<T>(Func<Guid, Task<T>> action)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthenticated<T>();
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

    private ActionResult<ApiResponse<T>> Unauthenticated<T>() =>
        this.FailEnvelope<T>("unauthorized", "Authentication is required.", StatusCodes.Status401Unauthorized);

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
