using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Insaaf.IntegrationTests.Infrastructure;

/// <summary>
/// The real API host on the SQL Server test database. One host serves the whole run.
/// Everything a run creates is tagged with <see cref="RunId"/> and removed again when the run ends.
/// </summary>
public sealed class SqlServerApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Tags this run's accounts (<c>it-{RunId}-…@example.com</c>) and restaurants (<c>IT-{RunId} …</c>).</summary>
    public string RunId { get; } = Guid.NewGuid().ToString("N")[..10];

    public string PhotoRoot { get; } = Path.Combine(Path.GetTempPath(), $"insaaf-it-photos-{Guid.NewGuid():N}");

    public string Outbox { get; } = Path.Combine(Path.GetTempPath(), $"insaaf-it-outbox-{Guid.NewGuid():N}");

    public string ConnectionString => IntegrationSettings.ConnectionString!;

    public string EmailPrefix => $"it-{RunId}-";

    public string RestaurantPrefix => $"IT-{RunId}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IntegrationSettings.EnsureSafeTarget();

        builder.UseEnvironment("Development");
        builder.UseSetting("UseInMemoryDatabase", "false");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "SqlServerIntegrationTestSigningKey_32Chars!");
        builder.UseSetting("Identity:SeedAdmin:Enabled", "false");
        builder.UseSetting("Storage:ReviewPhotos:RootPath", PhotoRoot);
        builder.UseSetting("Storage:ReviewPhotos:CleanupIntervalMinutes", "0");
        builder.UseSetting("Email:OutboxPath", Outbox);
        // All requests come from one test client, so every limit is raised (limits have their own API tests).
        foreach (var key in new[]
        {
            "VerificationResolvePerMinute", "VerificationCreatePerMinute", "SocialLikePerMinute", "SocialCommentPerMinute",
            "SocialFollowPerMinute", "SocialSearchPerMinute", "VerificationPhotoPerMinute", "AuthLoginPerMinute",
            "AuthRegisterPerMinute", "AuthRefreshPerMinute", "AuthForgotPerMinute", "AuthResetPerMinute",
        })
        {
            builder.UseSetting($"RateLimiting:{key}", "100000");
        }

        builder.ConfigureTestServices(services => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Commands)));
    }

    /// <summary>Counts the API's SQL commands (used by the N+1 checks).</summary>
    public SqlCommandCounter Commands { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Guard: the host must really be on the test database, never the development one.
        var actual = context.Database.GetDbConnection().Database;
        if (!string.Equals(actual, IntegrationSettings.DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            host.Dispose();
            throw new InvalidOperationException($"The API host is on database '{actual}', not '{IntegrationSettings.DatabaseName}'. Stopped.");
        }

        // The tests never migrate. A test database behind the code's migrations is reported, not fixed.
        var pending = context.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            host.Dispose();
            throw new InvalidOperationException(
                $"The test database '{actual}' is missing {pending.Count} migration(s): {string.Join(", ", pending)}. " +
                "Apply them yourself with: dotnet ef database update --connection \"<test connection string>\" " +
                "--project src/backend/Insaaf.Infrastructure/Insaaf.Infrastructure.csproj --startup-project src/backend/Insaaf.API/Insaaf.API.csproj");
        }

        // Roles (User, Admin) and the seeded restaurants are reference data, kept between runs.
        IdentitySeed.SeedAsync(host.Services).GetAwaiter().GetResult();
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (IntegrationSettings.ConnectionString is not null)
            {
                await TestDataCleanup.RemoveRunAsync(ConnectionString, EmailPrefix, RestaurantPrefix);
            }
        }
        finally
        {
            await base.DisposeAsync();
            if (Directory.Exists(PhotoRoot))
            {
                Directory.Delete(PhotoRoot, recursive: true);
            }

            if (Directory.Exists(Outbox))
            {
                Directory.Delete(Outbox, recursive: true);
            }
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerApiFactory>
{
    public const string Name = "SQL Server";
}
