using Microsoft.Net.Http.Headers;

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

            headers[HeaderNames.XContentTypeOptions] = "nosniff";
            headers[HeaderNames.XFrameOptions] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
            headers[HeaderNames.StrictTransportSecurity] = "max-age=31536000; includeSubDomains; preload";

            // Only add CSP for non-proxied responses (e.g., health, local errors, 404s)
            // Proxied responses should have their own CSP or none if they are pure APIs
            if (!headers.ContainsKey(HeaderNames.ContentSecurityPolicy) && !IsProxiedRequest(ctx))
            {
                headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; frame-ancestors 'none'";
            }

            // Information masking - redundant headers handled by host/Kestrel configuration
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await next(ctx);
    }


    private static bool IsProxiedRequest(HttpContext ctx)
    {
        // YARP sets IReverseProxyFeature for proxied requests
        return ctx.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>() != null;
    }

}
