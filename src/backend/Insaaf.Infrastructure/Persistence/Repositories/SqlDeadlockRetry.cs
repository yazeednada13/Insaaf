using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Retries a whole database operation when SQL Server picks it as a deadlock victim (error 1205). SQL
/// Server has already rolled the victim's transaction back, so a retry starts clean. Every statement in
/// these operations is conditional, so repeating one that had applied changes nothing. Storage calls are
/// never inside a retried operation.
/// </summary>
internal static class SqlDeadlockRetry
{
    private const int MaxAttempts = 3;

    public static async Task<T> RunAsync<T>(AppDbContext dbContext, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception exception) when (IsDeadlock(exception))
            {
                dbContext.ChangeTracker.Clear();
                if (attempt >= MaxAttempts)
                {
                    throw new RepositoryBusyException(exception);
                }

                await Task.Delay(Random.Shared.Next(20, 120) * attempt, cancellationToken);
            }
        }
    }

    private static bool IsDeadlock(Exception exception) =>
        exception is SqlException { Number: 1205 }
        || exception.InnerException is SqlException { Number: 1205 }
        || (exception is DbUpdateException && exception.InnerException?.InnerException is SqlException { Number: 1205 });
}
