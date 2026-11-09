using Insaaf.API.Extensions;
using Insaaf.API.Middleware;
using Insaaf.Application;
using Insaaf.Infrastructure;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Health;
using Insaaf.Infrastructure.Photos;
using System.Net;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Insaaf.Infrastructure.Persistence.Seeding;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddInsaafApiErrorResponses();
    builder.Services.AddInsaafRateLimiting(builder.Configuration);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddInsaafSwagger();
    // Phase 10 (10.8): liveness and readiness probes for a future host.
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    // Behind a reverse proxy (Phase 11), trust forwarded headers only from the configured proxies.
    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    if (knownProxies.Length > 0)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
    }

    // Phase 10 (F-7): outside Development, refuse a weak or committed JWT signing key.
    SecurityExtensions.EnsureProductionSigningKey(builder.Configuration, builder.Environment);

    var app = builder.Build();

    // Review photos must be stored outside the content root, which is inside the repository (Phase 9A).
    PhotoStorageOptions.EnsureOutsideContentRoot(
        app.Services.GetRequiredService<IOptions<PhotoStorageOptions>>().Value.ResolveRoot(),
        app.Environment.ContentRootPath);

    if (knownProxies.Length > 0)
    {
        app.UseForwardedHeaders();
    }

    app.UseExceptionHandling();
    app.UseInsaafSecurityHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Insaaf API v1");
            options.RoutePrefix = "swagger";
        });
    }

    if (!app.Configuration.GetValue<bool>("UseInMemoryDatabase"))
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication so verification limits are partitioned per user.
    app.UseRateLimiter();
    app.MapControllers();
    // Liveness: the process answers. Readiness: it can also reach the database. No details in the body.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        IdentitySeed.SeedAsync(app.Services).GetAwaiter().GetResult();
        RestaurantSeed.SeedAsync(app.Services).GetAwaiter().GetResult();
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
