using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SS.APIGateway.Extensions;

public static class RateLimitExtensions
{
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services, IConfiguration config)
    {
        var global = config.GetSection("RateLimiting:GlobalPolicy");
        var bruteForce = config.GetSection("RateLimiting:BruteForcePolicy");

        services.AddRateLimiter(options =>
        {
            // Global sliding window policy (per-IP, handles proxies)
            options.AddPolicy("global", ctx => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: GetClientIp(ctx),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = global.GetValue("PermitLimit", 200),
                    Window = global.GetValue("Window", TimeSpan.FromMinutes(1)),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = global.GetValue("QueueLimit", 50)
                }));

            // Brute-force protection for /api/auth/login and /api/mfa/verify
            options.AddPolicy("brute-force", ctx => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientIp(ctx),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = bruteForce.GetValue("PermitLimit", 5),
                    Window = bruteForce.GetValue("Window", TimeSpan.FromMinutes(1)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = bruteForce.GetValue("QueueLimit", 0)
                }));

            // Anti-abuse protection for registration and forgot-password
            var antiAbuse = config.GetSection("RateLimiting:AntiAbusePolicy");
            options.AddPolicy("anti-abuse", ctx => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientIp(ctx),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = antiAbuse.GetValue("PermitLimit", 10),
                    Window = antiAbuse.GetValue("Window", TimeSpan.FromMinutes(1)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = antiAbuse.GetValue("QueueLimit", 0)
                }));

            options.RejectionStatusCode = 429;
            options.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.Headers.RetryAfter = "60";
                await ctx.HttpContext.Response.WriteAsync(
                    """{"error":"too_many_requests","message":"Rate limit exceeded"}""", token);
            };
        });

        return services;
    }

    private static string GetClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

}
