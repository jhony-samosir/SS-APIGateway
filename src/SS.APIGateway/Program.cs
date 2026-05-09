using Microsoft.AspNetCore.HttpOverrides;
using SS.APIGateway.Configuration;
using SS.APIGateway.Extensions;
using SS.APIGateway.Middleware;
using SS.APIGateway.Transforms;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<InternalSignatureOptions>(builder.Configuration.GetSection("InternalSignature"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));

// ── Service Registration ─────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

builder.Services
    .AddGatewayAuthentication(builder.Configuration) // JWT RS256
    .AddGatewayRateLimiting(builder.Configuration)   // Global + BruteForce
    .AddGatewayCorsAndSecurity(builder.Configuration) // CORS + Security Headers
    .AddGatewayObservability(builder.Configuration)   // OpenTelemetry
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<IdentityTransformProvider>()
    .AddTransforms<InternalOriginSignatureTransform>()
    .AddResiliencyPolicies(builder.Configuration);

// ── Middleware Pipeline ──────────────────────────────────────────────────────
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors("GatewayPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health Check — anonymous access
app.MapHealthChecks("/health");

// YARP — must be last
app.MapReverseProxy();

app.Run();
