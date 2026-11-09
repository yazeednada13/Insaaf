using System.Text;
using Insaaf.Application.Admin;
using Insaaf.Infrastructure.Admin;
using Insaaf.Application.Photos.Abstractions;
using Insaaf.Infrastructure.Photos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Verification.Abstractions;
using Insaaf.Infrastructure.Verification;
using Insaaf.Domain.Repositories;
using Insaaf.Infrastructure.Auth;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Insaaf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useInMemoryDatabase = configuration.GetValue<bool>("UseInMemoryDatabase");
        var inMemoryDatabaseName = configuration["InMemoryDatabaseName"] ?? "Insaaf";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (useInMemoryDatabase)
            {
                options.UseInMemoryDatabase(inMemoryDatabaseName);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(IdentityConfiguration.ConfigureIdentity)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured via User Secrets or environment variables.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        services.AddAuthorization();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IVisitTokenRepository, VisitTokenRepository>();
        services.AddScoped<IReviewLikeRepository, ReviewLikeRepository>();
        services.AddScoped<IReviewCommentRepository, ReviewCommentRepository>();
        services.AddScoped<IUserFollowRepository, UserFollowRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserModerationRepository, UserModerationRepository>();
        services.AddScoped<ISavedRepository, SavedRepository>();
        services.AddScoped<IReviewCommentLikeRepository, ReviewCommentLikeRepository>();
        services.AddScoped<IReviewReportRepository, ReviewReportRepository>();
        services.AddScoped<IReviewReplyRepository, ReviewReplyRepository>();
        services.AddSingleton<IAdminAuditLog, AdminAuditLog>();

        // Phase 9A: review photos. Files live outside the repository (checked at startup in Program.cs).
        services.Configure<PhotoStorageOptions>(configuration.GetSection(PhotoStorageOptions.SectionName));
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddSingleton<IAvatarStorage, LocalAvatarStorage>();

        // Phase 11: email (password-reset codes). A local outbox unless SMTP is configured.
        services.Configure<Email.EmailOptions>(configuration.GetSection(Email.EmailOptions.SectionName));
        if (string.Equals(configuration["Email:Provider"], "smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, Email.SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, Email.FileEmailSender>();
        }
        services.AddScoped<IReviewPhotoRepository, ReviewPhotoRepository>();
        services.AddHostedService<ReviewPhotoCleanupHostedService>();
        services.AddSingleton<IVisitTokenCodec, VisitTokenCodec>();

        return services;
    }
}
