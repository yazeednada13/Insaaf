namespace Insaaf.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Insaaf";

    public string Audience { get; set; } = "Insaaf.Mobile";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public string SigningKey { get; set; } = string.Empty;
}
