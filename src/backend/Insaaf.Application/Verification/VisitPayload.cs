namespace Insaaf.Application.Verification;

/// <summary>
/// The only place that knows the QR payload format. The API always works with the extracted token,
/// so changing the printed format later (decision D-PL) does not affect redemption or contracts.
/// </summary>
public static class VisitPayload
{
    public const string Prefix = "insaaf://visit/";

    /// <summary>Base64url of 32 bytes without padding.</summary>
    public const int TokenLength = 43;

    public static string Build(string token) => $"{Prefix}{token}";

    /// <summary>Accepts a full payload or a bare token; rejects anything not shaped like an Insaaf token.</summary>
    public static bool TryExtractToken(string? input, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var candidate = input.Trim();
        if (candidate.StartsWith(Prefix, StringComparison.Ordinal))
        {
            candidate = candidate[Prefix.Length..];
        }

        if (candidate.Length != TokenLength || !candidate.All(IsBase64UrlChar))
        {
            return false;
        }

        token = candidate;
        return true;
    }

    private static bool IsBase64UrlChar(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
}
