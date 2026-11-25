using ClaimGuard.Application.Common;
using Microsoft.AspNetCore.Http;

namespace ClaimGuard.Api.Middleware;

/// <summary>Maps domain exceptions to RFC 7807-style problem details responses.</summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                DuplicateClaimException => (StatusCodes.Status409Conflict, "Duplicate claim"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error")
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "[v0] Unhandled exception on {Path}", context.Request.Path);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status,
                detail = status == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred. The incident has been logged."
                    : ex.Message
            });
        }
    }
}
