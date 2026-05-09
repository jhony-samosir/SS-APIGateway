using System.Security.Claims;
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
    // Headers that clients must NEVER be allowed to spoof
    private static readonly string[] _spoofableHeaders = [
        "X-User-Id",
        "X-User-Role",
        "X-User-Permissions",
        "X-User-PublicId",
        "X-Internal-Signature"
    ];

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        var requiresJwt = context.Route?.Metadata?.GetValueOrDefault("RequiresJwt") == "true";

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
            if (user.Identity?.IsAuthenticated != true) return ValueTask.CompletedTask;

            InjectHeader(transformCtx, "X-User-Id", user.FindFirstValue(ClaimConstants.UserId));
            InjectHeader(transformCtx, "X-User-PublicId", user.FindFirstValue(ClaimConstants.PublicId));
            InjectHeader(transformCtx, "X-User-Role", user.FindFirstValue(ClaimTypes.Role));
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
