namespace SS.APIGateway.Middleware;

/// <summary>
/// Adds OWASP-recommended security response headers to every response.
/// Uses OnStarting to ensure headers are set even if modified by downstream components.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var headers = ctx.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            // Never expose server info
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await next(ctx);
    }
}
