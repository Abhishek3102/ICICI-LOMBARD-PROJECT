using ClaimGuard.Application.Interfaces;

namespace ClaimGuard.Api.Middleware;

/// <summary>
/// Redis-backed fixed-window rate limiting (60 requests / minute / IP) across all
/// API routes. Works across replicas because the counter lives in Upstash Redis.
/// </summary>
public class RedisRateLimitMiddleware(RequestDelegate next, ICacheService cache)
{
    private const int MaxRequestsPerMinute = 60;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            if (await cache.IsRateLimitedAsync(clientKey, MaxRequestsPerMinute, TimeSpan.FromMinutes(1), context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Too many requests",
                    status = 429,
                    detail = "Rate limit exceeded. Try again in a minute."
                });
                return;
            }
        }

        await next(context);
    }
}
