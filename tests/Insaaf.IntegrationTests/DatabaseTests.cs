using System.Net;
using Insaaf.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Insaaf.IntegrationTests;

/// <summary>The test database itself: reachable, fully migrated, and configured like development.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class DatabaseTests
{
    private readonly SqlServerApiFactory _factory;
    private readonly Api _api;

    public DatabaseTests(SqlServerApiFactory factory)
    {
        _factory = factory;
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task ReadinessProbe_IsHealthy_OnSqlServer()
    {
        var response = await _api.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task ReadCommittedSnapshot_IsOn()
    {
        // The photo repository's locking rules (Phase 9A) were designed and probed with RCSI on.
        await using var connection = new SqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()", connection);

        Assert.True((bool)(await command.ExecuteScalarAsync())!, "READ_COMMITTED_SNAPSHOT is off on the test database.");
    }
}
