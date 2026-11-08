using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Insaaf.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"InsaafTests_{Guid.NewGuid():N}";

    /// <summary>Each test host stores photos in its own temporary folder, outside the repository.</summary>
    public string PhotoRoot { get; } = Path.Combine(Path.GetTempPath(), $"insaaf-api-photos-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("InMemoryDatabaseName", _databaseName);
        builder.UseSetting("Jwt:SigningKey", "IntegrationTestSigningKey_32CharsMin!");
        builder.UseSetting("Identity:SeedAdmin:Enabled", "false");
        builder.UseSetting("Storage:ReviewPhotos:RootPath", PhotoRoot);
        builder.UseSetting("Storage:ReviewPhotos:CleanupIntervalMinutes", "0");
        ConfigureTestSettings(builder);
    }

    /// <summary>Extra settings for specialised factories (e.g. low rate limits).</summary>
    protected virtual void ConfigureTestSettings(IWebHostBuilder builder)
    {
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        IdentitySeed.SeedAsync(host.Services).GetAwaiter().GetResult();
        RestaurantSeed.SeedAsync(host.Services).GetAwaiter().GetResult();

        return host;
    }
}
