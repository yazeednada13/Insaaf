using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Insaaf.IntegrationTests.Infrastructure;

/// <summary>
/// Where the integration tests find their database: <c>IntegrationTests:ConnectionString</c> from user secrets
/// or the environment variable <c>IntegrationTests__ConnectionString</c>. Without it every test is skipped.
/// </summary>
internal static class IntegrationSettings
{
    public const string ConnectionKey = "IntegrationTests:ConnectionString";

    /// <summary>The test database name must contain this, so the development database can never be used.</summary>
    public const string RequiredNamePart = "IntegrationTests";

    public static string? ConnectionString { get; } = Load();

    public static string MissingReason =>
        $"SQL Server integration tests are skipped: set {ConnectionKey} (user secrets) or the environment variable IntegrationTests__ConnectionString.";

    /// <summary>The database the connection string points at.</summary>
    public static string DatabaseName => new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;

    /// <summary>Refuses any database whose name doesn't mark it as the integration-test database.</summary>
    public static void EnsureSafeTarget()
    {
        if (ConnectionString is null)
        {
            throw new InvalidOperationException(MissingReason);
        }

        if (!DatabaseName.Contains(RequiredNamePart, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run: the integration-test database name must contain '{RequiredNamePart}' (got '{DatabaseName}').");
        }
    }

    private static string? Load()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(IntegrationSettings).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var value = configuration[ConnectionKey];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>A test that needs the SQL Server test database; skipped when it isn't configured.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (IntegrationSettings.ConnectionString is null)
        {
            Skip = IntegrationSettings.MissingReason;
        }
    }
}
