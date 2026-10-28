using Insaaf.Application.Common.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Extensions;

public static class ApiErrorResponseExtensions
{
    public static IServiceCollection AddInsaafApiErrorResponses(this IServiceCollection services)
    {
        // Model binding failures (e.g. priceLevel=abc) return the envelope instead of ProblemDetails.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(error => new ApiError
                    {
                        Code = "validation_error",
                        Message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The request is invalid."
                            : error.ErrorMessage,
                    })
                    .ToList();

                var envelope = ApiResponse<object>.Fail(
                    errors,
                    new ApiMeta { RequestId = context.HttpContext.TraceIdentifier });

                return new BadRequestObjectResult(envelope);
            };
        });

        // Missing or invalid JWT returns the envelope with code "unauthorized" instead of an empty body.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            options.Events.OnChallenge = async context =>
            {
                context.HandleResponse();

                var envelope = ApiResponse<object>.Fail(
                    [new ApiError { Code = "unauthorized", Message = "Authentication is required." }],
                    new ApiMeta { RequestId = context.HttpContext.TraceIdentifier });

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                await context.Response.WriteAsJsonAsync(envelope);
            };
        });

        return services;
    }
}
