using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewReportRepository : IReviewReportRepository
{
    private readonly AppDbContext _dbContext;

    public ReviewReportRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> AddIfNoOpenAsync(ReviewReport report, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.ReviewReports.AnyAsync(
                item => item.ReviewId == report.ReviewId && item.ReporterUserId == report.ReporterUserId && item.ResolvedAt == null,
                cancellationToken))
        {
            return false;
        }

        _dbContext.ReviewReports.Add(report);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request filed the same open report first; the filtered unique index kept one.
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<IReadOnlyList<AdminReportProjection>> ListAsync(
        bool? open,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default)
    {
        var reports = _dbContext.ReviewReports.AsNoTracking();
        if (open is { } isOpen)
        {
            reports = isOpen ? reports.Where(report => report.ResolvedAt == null) : reports.Where(report => report.ResolvedAt != null);
        }

        if (after is not null)
        {
            reports = reports.Where(report =>
                report.CreatedAt < after.CreatedAt || (report.CreatedAt == after.CreatedAt && report.Id < after.Id));
        }

        return await Project(reports.OrderByDescending(report => report.CreatedAt).ThenByDescending(report => report.Id).Take(take))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool?> ResolveAsync(Guid reportId, Guid adminId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.ReviewReports.SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        if (report.ResolvedAt is not null)
        {
            return false;
        }

        report.ResolvedAt = utcNow;
        report.ResolvedByUserId = adminId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<AdminReportProjection?> GetAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        Project(_dbContext.ReviewReports.AsNoTracking().Where(report => report.Id == reportId)).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<AdminReportProjection> Project(IQueryable<ReviewReport> reports) =>
        reports
            .Join(_dbContext.Reviews, report => report.ReviewId, review => review.Id, (report, review) => new { report, review })
            .Select(row => new AdminReportProjection(
                row.report.Id,
                row.report.ReviewId,
                row.review.RestaurantId,
                row.review.Restaurant.Name,
                row.report.ReporterUserId,
                _dbContext.Users.Where(user => user.Id == row.report.ReporterUserId).Select(user => user.UserName).FirstOrDefault(),
                row.report.Reason,
                row.report.Note,
                row.report.CreatedAt,
                row.report.ResolvedAt,
                row.review.HiddenAt != null));
}
