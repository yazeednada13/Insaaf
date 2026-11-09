using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IReviewReportService
{
    Task<ReviewReportedDto> ReportAsync(Guid reviewId, Guid reporterId, ReportReviewRequest? request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reporting a review (Phase 11, spec §17, D-7). It only files the report for the Admin queue: the review
/// stays visible until an Admin hides it. One open report per user per review.
/// </summary>
public sealed class ReviewReportService : IReviewReportService
{
    public const int MaxNoteLength = 500;

    private static readonly Dictionary<string, ReviewReportReason> Reasons = new(StringComparer.Ordinal)
    {
        ["offensive"] = ReviewReportReason.Offensive,
        ["not_genuine"] = ReviewReportReason.NotGenuine,
        ["wrong_information"] = ReviewReportReason.WrongInformation,
        ["other"] = ReviewReportReason.Other,
    };

    private readonly IReviewRepository _reviews;
    private readonly IReviewReportRepository _reports;
    private readonly IDateTimeProvider _clock;

    public ReviewReportService(IReviewRepository reviews, IReviewReportRepository reports, IDateTimeProvider clock)
    {
        _reviews = reviews;
        _reports = reports;
        _clock = clock;
    }

    public static string ReasonCode(ReviewReportReason reason) => Reasons.Single(pair => pair.Value == reason).Key;

    public async Task<ReviewReportedDto> ReportAsync(
        Guid reviewId,
        Guid reporterId,
        ReportReviewRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Reason is not { } code || !Reasons.TryGetValue(code, out var reason))
        {
            throw new SocialException("validation_error", "Reason must be offensive, not_genuine, wrong_information or other.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            throw new SocialException("validation_error", $"Note must be at most {MaxNoteLength} characters.");
        }

        if (!await _reviews.IsPublicAsync(reviewId, cancellationToken))
        {
            throw SocialException.ReviewNotFound();
        }

        await _reports.AddIfNoOpenAsync(
            new ReviewReport
            {
                Id = Guid.NewGuid(),
                ReviewId = reviewId,
                ReporterUserId = reporterId,
                Reason = reason,
                Note = note,
                CreatedAt = _clock.UtcNow,
            },
            cancellationToken);

        return new ReviewReportedDto(reviewId, true);
    }
}
