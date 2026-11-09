using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Application.Social.Validation;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface IReviewCommentService
{
    Task<ReviewCommentListResultDto> ListAsync(
        Guid reviewId,
        Guid viewerUserId,
        ReviewCommentsQuery query,
        CancellationToken cancellationToken = default);

    Task<ReviewCommentDto> CreateAsync(
        Guid reviewId,
        Guid userId,
        CreateReviewCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes the user's own comment. Others' comments are forbidden.</summary>
    Task<ReviewCommentDeletedDto> DeleteAsync(
        Guid reviewId,
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Flat comments on public reviews. Ownership is the only permission: there is no role check, and
/// Admin moderation of comments belongs to Phase 9.
/// </summary>
public sealed class ReviewCommentService : IReviewCommentService
{
    private readonly IReviewRepository _reviews;
    private readonly IReviewCommentRepository _comments;
    private readonly IDateTimeProvider _clock;
    private readonly INotificationPublisher _notifications;

    public ReviewCommentService(
        IReviewRepository reviews,
        IReviewCommentRepository comments,
        IDateTimeProvider clock,
        INotificationPublisher notifications)
    {
        _reviews = reviews;
        _comments = comments;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<ReviewCommentListResultDto> ListAsync(
        Guid reviewId,
        Guid viewerUserId,
        ReviewCommentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ThrowIfInvalid(ReviewCommentValidator.Validate(query));
        await EnsureReviewIsPublicAsync(reviewId, cancellationToken);

        var result = await _comments.ListByReviewAsync(reviewId, query.Page, query.PageSize, viewerUserId, cancellationToken);

        return new ReviewCommentListResultDto(
            result.Items.Select(comment => ToDto(comment, viewerUserId)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<ReviewCommentDto> CreateAsync(
        Guid reviewId,
        Guid userId,
        CreateReviewCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfInvalid(ReviewCommentValidator.Validate(request));
        await EnsureReviewIsPublicAsync(reviewId, cancellationToken);

        var comment = new ReviewComment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            UserId = userId,
            Text = request.Text!.Trim(),
            CreatedAt = _clock.UtcNow,
        };

        await _comments.AddAsync(comment, cancellationToken);
        await _notifications.CommentedAsync(reviewId, comment.Id, userId, cancellationToken);

        var created = await _comments.GetAsync(comment.Id, userId, cancellationToken);
        return ToDto(created!, userId);
    }

    public async Task<ReviewCommentDeletedDto> DeleteAsync(
        Guid reviewId,
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureReviewIsPublicAsync(reviewId, cancellationToken);

        var comment = await _comments.GetAsync(commentId, cancellationToken: cancellationToken);
        if (comment is null || comment.ReviewId != reviewId)
        {
            throw CommentNotFound();
        }

        if (comment.AuthorId != userId)
        {
            throw new SocialException(
                "comment_forbidden",
                "You can only delete your own comments.",
                SocialStatusCodes.Forbidden);
        }

        if (!await _comments.DeleteAsync(commentId, cancellationToken))
        {
            throw CommentNotFound();
        }

        await _notifications.CommentDeletedAsync(commentId, cancellationToken);

        return new ReviewCommentDeletedDto(reviewId, await _comments.CountAsync(reviewId, cancellationToken));
    }

    private async Task EnsureReviewIsPublicAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            throw SocialException.ReviewNotFound();
        }
    }

    private static void ThrowIfInvalid(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new SocialException("validation_error", string.Join(' ', errors));
        }
    }

    private static SocialException CommentNotFound() =>
        new("not_found", "Comment was not found.", SocialStatusCodes.NotFound);

    private static ReviewCommentDto ToDto(ReviewCommentProjection comment, Guid viewerUserId) =>
        new(
            comment.Id,
            comment.Text,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(comment.CreatedAt, DateTimeKind.Utc),
            UserAvatars.Reviewer(comment.AuthorId, comment.AuthorFullName, comment.AuthorUserName, comment.AuthorHasPublicProfile, comment.AuthorAvatarKey),
            comment.AuthorId == viewerUserId,
            comment.LikeCount,
            comment.IsLikedByViewer);
}
