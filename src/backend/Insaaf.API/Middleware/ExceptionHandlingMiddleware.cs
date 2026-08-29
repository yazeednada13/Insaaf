using System.Net;
using System.Text.Json;
using Insaaf.Application.Common.Models;

namespace Insaaf.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Developer logger
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            
            // User logger
            if (context.Response.HasStarted)
            {
                throw;
            }

            var statusCode = HttpStatusCode.InternalServerError;
            var errors = new List<ApiError>
            {
                new() { Code = "internal_error", Message = "An unexpected error occurred." }
            };

            var envelope = ApiResponse<object>.Fail(errors, CreateMeta(context));

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, JsonOptions));
        }
    }

    private static ApiMeta CreateMeta(HttpContext context) =>
        new() { RequestId = context.TraceIdentifier };
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
