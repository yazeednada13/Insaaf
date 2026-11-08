namespace Insaaf.Application.Verification;

public static class VerificationStatusCodes
{
    public const int BadRequest = 400;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Conflict = 409;
    public const int Gone = 410;
    public const int PayloadTooLarge = 413;
    public const int UnsupportedMediaType = 415;
    public const int ServerError = 500;
    public const int ServiceUnavailable = 503;
}

/// <summary>
/// Errors of the visit-token and verified-review flow. Codes: token_invalid, token_expired, token_used,
/// validation_error, not_found, restaurant_not_partner, issuance_forbidden.
/// </summary>
public sealed class VerificationException : Exception
{
    public VerificationException(string code, string message, int statusCode = VerificationStatusCodes.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
