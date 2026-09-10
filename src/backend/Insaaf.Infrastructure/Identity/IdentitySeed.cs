using Insaaf.Domain.Constants;
using Insaaf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insaaf.Infrastructure.Identity;

public static class IdentitySeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        await EnsureRoleAsync(roleManager, Roles.User, cancellationToken);
        await EnsureRoleAsync(roleManager, Roles.Admin, cancellationToken);

        var seedAdminEnabled = configuration.GetValue<bool>("Identity:SeedAdmin:Enabled");
        if (!seedAdminEnabled)
        {
            return;
        }

        var adminEmail = configuration["Identity:SeedAdmin:Email"];
        var adminPassword = configuration["Identity:SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Identity:SeedAdmin:Enabled is true but email or password is missing.");
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is not null)
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, Roles.Admin))
            {
                await userManager.AddToRoleAsync(existingAdmin, Roles.Admin);
            }

            return;
        }

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            logger.LogWarning(
                "Failed to seed development admin user: {Errors}",
                string.Join(", ", createResult.Errors.Select(error => error.Description)));
            return;
        }

        await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        logger.LogInformation("Development admin user seeded for {Email}", adminEmail);
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = roleName,
        });

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
        }
    }
}
