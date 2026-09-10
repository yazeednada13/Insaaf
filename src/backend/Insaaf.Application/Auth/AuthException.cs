namespace Insaaf.Application.Auth;

public static class AuthStatusCodes
{
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
}

public sealed class AuthException : Exception
{
    public AuthException(string code, string message, int statusCode = AuthStatusCodes.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
