namespace Insaaf.Application.Admin;

public static class AdminStatusCodes
{
    public const int BadRequest = 400;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int ServiceUnavailable = 503;
}

/// <summary>An Admin operation that can't be done; the controller turns it into the envelope.</summary>
public sealed class AdminException : Exception
{
    public AdminException(string code, string message, int statusCode = AdminStatusCodes.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    public static AdminException NotFound(string what) =>
        new("not_found", $"{what} was not found.", AdminStatusCodes.NotFound);
}
