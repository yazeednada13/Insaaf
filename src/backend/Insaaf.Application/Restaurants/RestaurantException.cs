namespace Insaaf.Application.Restaurants;

public static class RestaurantStatusCodes
{
    public const int BadRequest = 400;
    public const int NotFound = 404;
}

public sealed class RestaurantException : Exception
{
    public RestaurantException(
        string code,
        string message,
        int statusCode = RestaurantStatusCodes.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
