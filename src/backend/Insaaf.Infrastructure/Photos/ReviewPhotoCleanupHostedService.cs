using Insaaf.Application.Photos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Photos;

/// <summary>
/// Runs the photo cleanup on a timer (hourly by default; disabled when the interval is 0). The first run
/// waits one interval, so startup is never slowed. Only counts are logged: no ids beyond the run, no
/// paths, no content.
/// </summary>
public sealed class ReviewPhotoCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReviewPhotoCleanupHostedService> _logger;
    private readonly TimeSpan _interval;

    public ReviewPhotoCleanupHostedService(
        IServiceScopeFactory scopes,
        IOptions<PhotoStorageOptions> options,
        ILogger<ReviewPhotoCleanupHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(0, options.Value.CleanupIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval == TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IReviewPhotoCleanupService>();
                var report = await cleanup.RunOnceAsync(stoppingToken);
                _logger.LogInformation(
                    "Photo cleanup: {Tombstoned} tombstoned, {Purged} purged, {FilesSwept} files and {FoldersSwept} folders swept, {Pruned} pruned, {CountersRepaired} counters repaired, {Failures} failures",
                    report.Tombstoned,
                    report.Purged,
                    report.FilesSwept,
                    report.FoldersSwept,
                    report.Pruned,
                    report.CountersRepaired,
                    report.Failures);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError("Photo cleanup run failed: {ExceptionType}", exception.GetType().Name);
            }
        }
    }
}
