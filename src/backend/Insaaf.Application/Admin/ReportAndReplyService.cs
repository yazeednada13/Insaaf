using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Admin;

public interface IReportAndReplyService
{
    Task<AdminReportListResultDto> ListReportsAsync(AdminReportListQuery query, CancellationToken cancellationToken = default);

    Task<AdminReportDto> ResolveReportAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default);

    Task<AdminReviewReplyDto> SetReplyAsync(Guid actorId, Guid reviewId, SetReviewReplyRequest? request, CancellationToken cancellationToken = default);

    Task<AdminReplyDeletedDto> DeleteReplyAsync(Guid actorId, Guid reviewId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The Admin side of Phase 11 moderation. Reports are a queue: resolving one never changes the review (an
/// Admin hides it separately). Official replies are written by an Admin in the restaurant's name (ADR-021)
/// and never change the review or its rating (spec §27). Audit lines carry ids and results only.
/// </summary>
public sealed class ReportAndReplyService : IReportAndReplyService
{
    public const int MaxLimit = 50;
    public const int MaxReplyLength = 1000;

    private readonly IReviewReportRepository _reports;
    private readonly IReviewReplyRepository _replies;
    private readonly IReviewRepository _reviews;
    private readonly INotificationPublisher _notifications;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLog _audit;

    public ReportAndReplyService(
        IReviewReportRepository reports,
        IReviewReplyRepository replies,
        IReviewRepository reviews,
        INotificationPublisher notifications,
        IDateTimeProvider clock,
        IAdminAuditLog audit)
    {
        _reports = reports;
        _replies = replies;
        _reviews = reviews;
        _notifications = notifications;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AdminReportListResultDto> ListReportsAsync(AdminReportListQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new AdminException("validation_error", $"Limit must be between 1 and {MaxLimit}.");
        }

        bool? open = query.Status switch
        {
            null or "open" => true,
            "resolved" => false,
            "all" => null,
            _ => throw new AdminException("validation_error", "Status must be open, resolved or all."),
        };

        FeedCursor? after;
        try
        {
            after = FeedCursorCodec.Decode(query.Cursor);
        }
        catch (SocialException)
        {
            throw new AdminException("validation_error", "Cursor is invalid.");
        }

        var rows = await _reports.ListAsync(open, after, query.Limit + 1, cancellationToken);
        var items = rows.Take(query.Limit).ToList();
        var next = rows.Count > query.Limit ? FeedCursorCodec.Encode(items[^1].CreatedAt, items[^1].Id) : null;
        return new AdminReportListResultDto(items.Select(ToDto).ToList(), next);
    }

    public async Task<AdminReportDto> ResolveReportAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var changed = await _reports.ResolveAsync(reportId, actorId, _clock.UtcNow, cancellationToken);
        Record("report.resolve", actorId, "report", reportId, changed switch
        {
            null => AdminAuditResult.NotFound,
            true => AdminAuditResult.Succeeded,
            false => AdminAuditResult.NoChange,
        });

        return ToDto(await _reports.GetAsync(reportId, cancellationToken) ?? throw AdminException.NotFound("Report"));
    }

    public async Task<AdminReviewReplyDto> SetReplyAsync(
        Guid actorId,
        Guid reviewId,
        SetReviewReplyRequest? request,
        CancellationToken cancellationToken = default)
    {
        var text = request?.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxReplyLength)
        {
            Record("review.reply", actorId, "review", reviewId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", $"Text is required and must be at most {MaxReplyLength} characters.");
        }

        // Only a visible review can carry a visible reply.
        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            Record("review.reply", actorId, "review", reviewId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("Review");
        }

        var created = await _replies.UpsertAsync(reviewId, text, actorId, _clock.UtcNow, cancellationToken);
        if (created)
        {
            // Only a new reply notifies; replacing its text doesn't notify again.
            await _notifications.RepliedAsync(reviewId, actorId, cancellationToken);
        }

        Record("review.reply", actorId, "review", reviewId, AdminAuditResult.Succeeded);
        var reply = await _replies.GetAsync(reviewId, cancellationToken) ?? throw AdminException.NotFound("Reply");
        return new AdminReviewReplyDto(
            reply.ReviewId,
            reply.Text,
            DateTime.SpecifyKind(reply.CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(reply.UpdatedAt, DateTimeKind.Utc));
    }

    public async Task<AdminReplyDeletedDto> DeleteReplyAsync(Guid actorId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        if (!await _replies.DeleteAsync(reviewId, cancellationToken))
        {
            Record("review.reply.delete", actorId, "review", reviewId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("Reply");
        }

        await _notifications.ReplyDeletedAsync(reviewId, cancellationToken);
        Record("review.reply.delete", actorId, "review", reviewId, AdminAuditResult.Succeeded);
        return new AdminReplyDeletedDto(reviewId, true);
    }

    private void Record(string action, Guid actorId, string targetType, Guid targetId, string result) =>
        _audit.Record(new AdminAuditEntry(action, actorId, targetType, targetId, _clock.UtcNow, result));

    private static AdminReportDto ToDto(AdminReportProjection report) =>
        new(
            report.Id,
            report.ReviewId,
            report.RestaurantId,
            report.RestaurantName,
            report.ReporterUserId,
            report.ReporterUserName,
            ReviewReportService.ReasonCode(report.Reason),
            report.Note,
            DateTime.SpecifyKind(report.CreatedAt, DateTimeKind.Utc),
            report.ResolvedAt is { } resolved ? DateTime.SpecifyKind(resolved, DateTimeKind.Utc) : null,
            report.IsReviewHidden);
}
