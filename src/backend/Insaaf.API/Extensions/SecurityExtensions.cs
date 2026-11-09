using System.Text;

namespace Insaaf.API.Extensions;

/// <summary>Phase 10 hardening: startup safety checks and response headers.</summary>
public static class SecurityExtensions
{
    // Markers of the committed development key (appsettings.Development.json). Never valid elsewhere.
    private static readonly string[] DevelopmentKeyMarkers = ["DevelopmentOnly", "ChangeViaUserSecrets"];

    private const int MinimumKeyBytes = 32;

    /// <summary>
    /// Outside Development, refuses to start with a short JWT signing key or the committed development key
    /// (finding F-7). The real key comes from an environment variable or a secret store.
    /// </summary>
    public static void EnsureProductionSigningKey(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var key = configuration["Jwt:SigningKey"] ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes
            || DevelopmentKeyMarkers.Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a private key of at least 32 bytes outside Development; the development key is refused.");
        }
    }

    /// <summary>
    /// Conservative headers for a JSON API used by a mobile app (finding F-8). Swagger (Development only)
    /// keeps its own page, so the restrictive content policy isn't applied to it.
    /// </summary>
    public static IApplicationBuilder UseInsaafSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            await next();
        });
}
