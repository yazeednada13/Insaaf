using Insaaf.API.Extensions;
using Insaaf.API.Middleware;
using Insaaf.Application;
using Insaaf.Infrastructure;
using Insaaf.Infrastructure.Identity;
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
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddInsaafSwagger();

    var app = builder.Build();

    app.UseExceptionHandling();

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
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        IdentitySeed.SeedAsync(app.Services).GetAwaiter().GetResult();
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
