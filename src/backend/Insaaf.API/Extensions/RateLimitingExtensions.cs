using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Insaaf.Application.Common.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Insaaf.API.Extensions;

/// <summary>
/// Built-in ASP.NET Core rate limiting for the verification and social endpoints (guessing and spam).
/// Limits are per authenticated user, configurable under "RateLimiting".
/// </summary>
public static class RateLimitingExtensions
{
    public const string VerificationResolvePolicy = "verification-resolve";
    public const string VerificationCreatePolicy = "verification-create";
    public const string SocialLikePolicy = "social-like";
    public const string SocialCommentPolicy = "social-comment";
    public const string SocialFollowPolicy = "social-follow";
    public const string SocialSearchPolicy = "social-search";
    public const string VerificationPhotoPolicy = "verification-photo";

    public static IServiceCollection AddInsaafRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var resolvePerMinute = configuration.GetValue("RateLimiting:VerificationResolvePerMinute", 20);
        var createPerMinute = configuration.GetValue("RateLimiting:VerificationCreatePerMinute", 5);
        var likesPerMinute = configuration.GetValue("RateLimiting:SocialLikePerMinute", 60);
        var commentsPerMinute = configuration.GetValue("RateLimiting:SocialCommentPerMinute", 10);
        var followsPerMinute = configuration.GetValue("RateLimiting:SocialFollowPerMinute", 30);
        var searchesPerMinute = configuration.GetValue("RateLimiting:SocialSearchPerMinute", 60);
        var photosPerMinute = configuration.GetValue("RateLimiting:VerificationPhotoPerMinute", 30);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var envelope = ApiResponse<object>.Fail(
                    [new ApiError { Code = "rate_limited", Message = "Too many requests. Try again shortly." }],
                    new ApiMeta { RequestId = context.HttpContext.TraceIdentifier });
                await context.HttpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
            };

            options.AddPolicy(VerificationResolvePolicy, httpContext => PerUserFixedWindow(httpContext, resolvePerMinute));
            options.AddPolicy(VerificationCreatePolicy, httpContext => PerUserFixedWindow(httpContext, createPerMinute));
            options.AddPolicy(SocialLikePolicy, httpContext => PerUserFixedWindow(httpContext, likesPerMinute));
            options.AddPolicy(SocialCommentPolicy, httpContext => PerUserFixedWindow(httpContext, commentsPerMinute));
            options.AddPolicy(SocialFollowPolicy, httpContext => PerUserFixedWindow(httpContext, followsPerMinute));
            options.AddPolicy(SocialSearchPolicy, httpContext => PerUserFixedWindow(httpContext, searchesPerMinute));
            options.AddPolicy(VerificationPhotoPolicy, httpContext => PerUserFixedWindow(httpContext, photosPerMinute));
        });

        return services;
    }

    private static RateLimitPartition<string> PerUserFixedWindow(HttpContext httpContext, int permitsPerMinute)
    {
        var partitionKey = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
