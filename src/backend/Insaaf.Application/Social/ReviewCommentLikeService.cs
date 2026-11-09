using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IReviewCommentLikeService
{
    Task<ReviewCommentLikeStateDto> LikeAsync(Guid reviewId, Guid commentId, Guid userId, CancellationToken cancellationToken = default);

    Task<ReviewCommentLikeStateDto> UnlikeAsync(Guid reviewId, Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Comment likes (Phase 11, Figma «إعجاب · N»). No notification, and no effect on any rating.</summary>
public sealed class ReviewCommentLikeService : IReviewCommentLikeService
{
    private readonly IReviewRepository _reviews;
    private readonly IReviewCommentRepository _comments;
    private readonly IReviewCommentLikeRepository _likes;
    private readonly IDateTimeProvider _clock;

    public ReviewCommentLikeService(
        IReviewRepository reviews,
        IReviewCommentRepository comments,
        IReviewCommentLikeRepository likes,
        IDateTimeProvider clock)
    {
        _reviews = reviews;
        _comments = comments;
        _likes = likes;
        _clock = clock;
    }

    public async Task<ReviewCommentLikeStateDto> LikeAsync(
        Guid reviewId,
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVisibleAsync(reviewId, commentId, cancellationToken);
        await _likes.AddIfMissingAsync(
            new ReviewCommentLike { CommentId = commentId, UserId = userId, CreatedAt = _clock.UtcNow },
            cancellationToken);
        return new ReviewCommentLikeStateDto(commentId, await _likes.CountAsync(commentId, cancellationToken), true);
    }

    public async Task<ReviewCommentLikeStateDto> UnlikeAsync(
        Guid reviewId,
        Guid commentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVisibleAsync(reviewId, commentId, cancellationToken);
        await _likes.RemoveIfExistsAsync(commentId, userId, cancellationToken);
        return new ReviewCommentLikeStateDto(commentId, await _likes.CountAsync(commentId, cancellationToken), false);
    }

    // The comment must belong to this review, and the review must be public.
    private async Task EnsureVisibleAsync(Guid reviewId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            throw SocialException.ReviewNotFound();
        }

        var comment = await _comments.GetAsync(commentId, cancellationToken: cancellationToken);
        if (comment is null || comment.ReviewId != reviewId)
        {
            throw new SocialException("not_found", "Comment was not found.", SocialStatusCodes.NotFound);
        }
    }
}
