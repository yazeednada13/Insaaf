using Insaaf.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Extensions;

public static class ApiResponseExtensions
{
    public static ApiMeta CreateMeta(this ControllerBase controller) =>
        new() { RequestId = controller.HttpContext.TraceIdentifier };

    public static ActionResult<ApiResponse<T>> OkEnvelope<T>(this ControllerBase controller, T data) =>
        controller.Ok(ApiResponse<T>.Ok(data, controller.CreateMeta()));

    public static ActionResult<ApiResponse<T>> FailEnvelope<T>(
        this ControllerBase controller,
        string code,
        string message,
        int statusCode)
    {
        var envelope = ApiResponse<T>.Fail(
            [new ApiError { Code = code, Message = message }],
            controller.CreateMeta());

        return controller.StatusCode(statusCode, envelope);
    }
}
