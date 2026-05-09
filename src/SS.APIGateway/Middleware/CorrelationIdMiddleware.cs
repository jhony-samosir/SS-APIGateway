namespace SS.APIGateway.Middleware;

/// <summary>
/// Generates or forwards X-Correlation-Id for distributed tracing.
/// Validates client-supplied IDs to prevent log/trace injection.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var rawCorrelationId = ctx.Request.Headers[CorrelationHeader].FirstOrDefault();
        
        // Validate: must be a valid GUID/UUID string or we generate a new one
        var correlationId = (rawCorrelationId != null && Guid.TryParse(rawCorrelationId, out var guid))
            ? guid.ToString("N")
            : Guid.NewGuid().ToString("N");

        ctx.Request.Headers[CorrelationHeader] = correlationId;
        ctx.Response.Headers[CorrelationHeader] = correlationId;

        // Make correlation ID available in logging scope
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();

        // Structured log — NO PII, NO tokens
        logger.LogInformation(
            "Gateway request completed: {Method} {Path} → {StatusCode} in {DurationMs}ms [cid={CorrelationId}]",
            ctx.Request.Method,
            ctx.Request.Path,
            ctx.Response.StatusCode,
            sw.ElapsedMilliseconds,
            correlationId);
    }
}
