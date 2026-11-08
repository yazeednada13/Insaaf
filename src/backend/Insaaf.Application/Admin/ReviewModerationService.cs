using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Photos;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Social;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Admin;

public interface IReviewModerationService
{
    Task<AdminReviewListResultDto> ListAsync(AdminReviewListQuery query, CancellationToken cancellationToken = default);

    Task<AdminReviewDto> SetVisibilityAsync(
        Guid actorId,
        Guid reviewId,
        SetReviewVisibilityRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminCommentDeletedDto> DeleteCommentAsync(Guid actorId, Guid commentId, CancellationToken cancellationToken = default);

    /// <summary>Removes any review photo (Phase 9A, task 9A.4).</summary>
    Task<PhotoRemovedDto> DeletePhotoAsync(Guid actorId, Guid photoId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Review and comment moderation (Phase 9, tasks 9.2b, 9.3 and 9.5). Hiding a review removes it from
/// every public read and from likes and comments through <c>ReviewVisibility.IsPublic</c>; its visit
/// token stays redeemed. Comments are deleted permanently, as when their author deletes them (ADR-012).
/// </summary>
public sealed class ReviewModerationService : IReviewModerationService
{
    public const int MaxLimit = 50;
    public const int MaxReasonLength = 500;

    private readonly IReviewRepository _reviews;
    private readonly IReviewCommentRepository _comments;
    private readonly INotificationPublisher _notifications;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLog _audit;
    private readonly IReviewPhotoRepository _photos;
    private readonly ReviewPhotoRemoval _photoRemoval;

    public ReviewModerationService(
        IReviewRepository reviews,
        IReviewCommentRepository comments,
        INotificationPublisher notifications,
        IDateTimeProvider clock,
        IAdminAuditLog audit,
        IReviewPhotoRepository photos,
        ReviewPhotoRemoval photoRemoval)
    {
        _reviews = reviews;
        _comments = comments;
        _notifications = notifications;
        _clock = clock;
        _audit = audit;
        _photos = photos;
        _photoRemoval = photoRemoval;
    }

    public async Task<PhotoRemovedDto> DeletePhotoAsync(Guid actorId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _photos.GetAsync(photoId, cancellationToken);
        string? fileDeletion = null;
        if (photo is not null)
        {
            try
            {
                fileDeletion = await _photoRemoval.RemoveAsync(photo, actorId, cancellationToken);
            }
            catch (RepositoryBusyException)
            {
                throw new AdminException("try_again", "Please try again.", AdminStatusCodes.ServiceUnavailable);
            }
        }

        // The audit says whether the files are really gone; it is never rewritten later (plan section 8).
        var result = fileDeletion switch
        {
            null => AdminAuditResult.NotFound,
            FileDeletionState.Completed => AdminAuditResult.Succeeded,
            _ => AdminAuditResult.RemovedFilePending,
        };
        _audit.Record(new AdminAuditEntry("photo.remove", actorId, "photo", photoId, _clock.UtcNow, result));

        return fileDeletion is null
            ? throw AdminException.NotFound("Photo")
            : new PhotoRemovedDto(photoId, true, fileDeletion);
    }

    public async Task<AdminReviewListResultDto> ListAsync(
        AdminReviewListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new AdminException("validation_error", $"Limit must be between 1 and {MaxLimit}.");
        }

        FeedCursor? after;
        try
        {
            after = FeedCursorCodec.Decode(query.Cursor);
        }
        catch (SocialException)
        {
            throw new AdminException("validation_error", "Cursor is invalid.");
        }

        // One extra row tells whether another page exists.
        var rows = await _reviews.ListForAdminAsync(query.RestaurantId, query.UserId, after, query.Limit + 1, cancellationToken);
        var items = rows.Take(query.Limit).ToList();
        var next = rows.Count > query.Limit ? FeedCursorCodec.Encode(items[^1].CreatedAt, items[^1].Id) : null;

        return new AdminReviewListResultDto(items.Select(ToDto).ToList(), next);
    }

    public async Task<AdminReviewDto> SetVisibilityAsync(
        Guid actorId,
        Guid reviewId,
        SetReviewVisibilityRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Hidden is not { } hidden)
        {
            Record("review.visibility", actorId, reviewId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", "hidden is required.");
        }

        var action = hidden ? "review.hide" : "review.unhide";
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > MaxReasonLength })
        {
            Record(action, actorId, reviewId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", $"Reason must be at most {MaxReasonLength} characters.");
        }

        var changed = await _reviews.SetHiddenAsync(reviewId, hidden, actorId, reason, _clock.UtcNow, cancellationToken);
        if (changed is null)
        {
            Record(action, actorId, reviewId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("Review");
        }

        Record(action, actorId, reviewId, changed.Value ? AdminAuditResult.Succeeded : AdminAuditResult.NoChange);
        return ToDto(await _reviews.GetForAdminAsync(reviewId, cancellationToken) ?? throw AdminException.NotFound("Review"));
    }

    public async Task<AdminCommentDeletedDto> DeleteCommentAsync(
        Guid actorId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        // Works on any review, hidden or not: no visibility check here.
        if (!await _comments.DeleteAsync(commentId, cancellationToken))
        {
            _audit.Record(new AdminAuditEntry("comment.delete", actorId, "comment", commentId, _clock.UtcNow, AdminAuditResult.NotFound));
            throw AdminException.NotFound("Comment");
        }

        await _notifications.CommentDeletedAsync(commentId, cancellationToken);
        _audit.Record(new AdminAuditEntry("comment.delete", actorId, "comment", commentId, _clock.UtcNow, AdminAuditResult.Succeeded));
        return new AdminCommentDeletedDto(commentId, true);
    }

    private void Record(string action, Guid actorId, Guid reviewId, string result) =>
        _audit.Record(new AdminAuditEntry(action, actorId, "review", reviewId, _clock.UtcNow, result));

    private static AdminReviewDto ToDto(AdminReviewProjection item) =>
        new(
            item.Id,
            item.RestaurantId,
            item.RestaurantName,
            item.AuthorId,
            item.AuthorUserName,
            item.Rating,
            item.Text,
            item.CreatedAt,
            item.HiddenAt is not null,
            item.HiddenAt,
            item.HiddenReason,
            item.CommentCount);
}
