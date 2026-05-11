using System.Security.Claims;
using Microsoft.Extensions.Logging;
using SS.APIGateway.Common;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SS.APIGateway.Transforms;

/// <summary>
/// Custom YARP Transform Provider:
/// 1. STRIPS client-supplied identity headers (Header Spoofing Protection)
/// 2. INJECTS clean X-User-* headers from validated JWT claims
/// Only executes on routes where RequiresJwt = "true" in metadata.
/// </summary>
public sealed class IdentityTransformProvider : ITransformProvider
{
    private readonly ILogger<IdentityTransformProvider> _logger;

    public IdentityTransformProvider(ILogger<IdentityTransformProvider> logger)
    {
        _logger = logger;
    }

    // Headers that clients must NEVER be allowed to spoof
    private static readonly string[] _spoofableHeaders = [
        "X-User-Id",
        "X-User-Roles",
        "X-User-Permissions",
        "X-User-PublicId",
        "X-Internal-Signature",
        "Authorization" // Strip client-supplied auth to ensure we only use validated cookie/token
    ];

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        var requiresJwt = context.Route?.Metadata?.GetValueOrDefault("RequiresJwt") == "true";
        var routeId = context.Route?.RouteId ?? "Unknown";

        // Always strip spoofable headers regardless of route type
        foreach (var header in _spoofableHeaders)
        {
            context.AddRequestHeaderRemove(header);
        }

        if (!requiresJwt) return;

        // Inject validated identity from JWT claims into clean internal headers
        context.AddRequestTransform(transformCtx =>
        {
            var user = transformCtx.HttpContext.User;
            var path = transformCtx.HttpContext.Request.Path;
            
            // 1. Auto-propagate accessToken cookie to Authorization header (Primary Identity)
            if (transformCtx.HttpContext.Request.Cookies.TryGetValue("accessToken", out var token))
            {
                _logger.LogDebug("[Auth] Injecting Bearer token from accessToken cookie for {Path}", path);
                
                // Clear any leftover Authorization header just in case, then add our validated one
                transformCtx.ProxyRequest.Headers.Remove("Authorization");
                transformCtx.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            }
            else
            {
                _logger.LogWarning("[Auth] No accessToken cookie found for protected route {RouteId} at {Path}", routeId, path);
            }

            // 2. Inject identity claims (Internal Consumption)
            if (user.Identity?.IsAuthenticated != true)
            {
                _logger.LogTrace("[Auth] User is not authenticated for {Path}", path);
                return ValueTask.CompletedTask;
            }

            _logger.LogDebug("[Auth] Injecting identity headers for User:{UserId} on {Path}", 
                user.FindFirstValue(ClaimConstants.UserId), path);

            InjectHeader(transformCtx, "X-User-Id", user.FindFirstValue(ClaimConstants.UserId));
            InjectHeader(transformCtx, "X-User-PublicId", user.FindFirstValue(ClaimConstants.PublicId));
            InjectHeader(transformCtx, "X-User-Roles", user.FindFirstValue(ClaimConstants.Role));
            InjectHeader(transformCtx, "X-User-Permissions", user.FindFirstValue(ClaimConstants.Permissions));

            return ValueTask.CompletedTask;
        });
    }

    private static void InjectHeader(RequestTransformContext ctx, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ctx.ProxyRequest.Headers.TryAddWithoutValidation(name, value);
    }
}
