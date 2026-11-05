namespace Insaaf.Application.Social;

public static class SocialStatusCodes
{
    public const int BadRequest = 400;
    public const int Forbidden = 403;
    public const int NotFound = 404;
}

/// <summary>Errors of likes and comments. Codes: validation_error, not_found, comment_forbidden.</summary>
public sealed class SocialException : Exception
{
    public SocialException(string code, string message, int statusCode = SocialStatusCodes.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    public static SocialException ReviewNotFound() =>
        new("not_found", "Review was not found.", SocialStatusCodes.NotFound);
}
