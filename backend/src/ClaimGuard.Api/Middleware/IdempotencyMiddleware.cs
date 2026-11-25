using ClaimGuard.Application.Interfaces;
using ClaimGuard.Application.Services;

namespace ClaimGuard.Api.Middleware;

/// <summary>
/// OWASP-safe duplicate protection for claim submission:
/// 1. Requires an X-Idempotency-Key header on POST /api/claims.
/// 2. Rejects repeats early with 409 using the Redis lock.
/// 3. Stashes the key for the controller; the DB unique constraint is the final backstop.
/// </summary>
public class IdempotencyMiddleware(RequestDelegate next, ICacheService cache)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var isClaimSubmission =
            HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/api/claims");

        if (isClaimSubmission)
        {
            var key = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(key))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Validation failed",
                    status = 400,
                    detail = "Header X-Idempotency-Key is required for claim submission."
                });
                return;
            }

            if (key.Length > 100)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Validation failed",
                    status = 400,
                    detail = "X-Idempotency-Key must be at most 100 characters."
                });
                return;
            }

            if (await cache.HasIdempotencyLockAsync(ClaimService.IdempotencyKeyPrefix + key, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Duplicate claim",
                    status = 409,
                    detail = "This idempotency key was already used. Retrieve the original claim instead of resubmitting."
                });
                return;
            }

            context.Items["IdempotencyKey"] = key;
        }

        await next(context);
    }
}
