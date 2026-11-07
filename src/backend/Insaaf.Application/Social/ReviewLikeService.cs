using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IReviewLikeService
{
    /// <summary>Likes a review. Repeating it changes nothing.</summary>
    Task<ReviewLikeStateDto> LikeAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Removes the user's like. Repeating it changes nothing.</summary>
    Task<ReviewLikeStateDto> UnlikeAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Likes never touch ratings: they live in their own table and aggregates don't read it.</summary>
public sealed class ReviewLikeService : IReviewLikeService
{
    private readonly IReviewRepository _reviews;
    private readonly IReviewLikeRepository _likes;
    private readonly IDateTimeProvider _clock;
    private readonly INotificationPublisher _notifications;

    public ReviewLikeService(
        IReviewRepository reviews,
        IReviewLikeRepository likes,
        IDateTimeProvider clock,
        INotificationPublisher notifications)
    {
        _reviews = reviews;
        _likes = likes;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<ReviewLikeStateDto> LikeAsync(
        Guid reviewId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureReviewIsPublicAsync(reviewId, cancellationToken);

        await _likes.AddIfMissingAsync(
            new ReviewLike { ReviewId = reviewId, UserId = userId, CreatedAt = _clock.UtcNow },
            cancellationToken);
        await _notifications.LikedAsync(reviewId, userId, cancellationToken);

        return new ReviewLikeStateDto(reviewId, await _likes.CountAsync(reviewId, cancellationToken), true);
    }

    public async Task<ReviewLikeStateDto> UnlikeAsync(
        Guid reviewId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureReviewIsPublicAsync(reviewId, cancellationToken);

        await _likes.RemoveIfExistsAsync(reviewId, userId, cancellationToken);
        await _notifications.UnlikedAsync(reviewId, userId, cancellationToken);

        return new ReviewLikeStateDto(reviewId, await _likes.CountAsync(reviewId, cancellationToken), false);
    }

    private async Task EnsureReviewIsPublicAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            throw SocialException.ReviewNotFound();
        }
    }
}
