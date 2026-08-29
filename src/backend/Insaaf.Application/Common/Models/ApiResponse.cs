namespace Insaaf.Application.Common.Models;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public IReadOnlyList<ApiError>? Errors { get; init; }

    public ApiMeta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, ApiMeta? meta = null) =>
        new() { Success = true, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(IReadOnlyList<ApiError> errors, ApiMeta? meta = null) =>
        new() { Success = false, Errors = errors, Meta = meta };
}
