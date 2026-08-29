namespace Insaaf.Application.Common.Models;

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
