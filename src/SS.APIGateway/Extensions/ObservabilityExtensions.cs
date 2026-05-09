using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

namespace SS.APIGateway.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Configures OpenTelemetry tracing + metrics + structured JSON logging.
    /// Exports to OTEL Collector via gRPC (OTLP).
    /// </summary>
    public static IServiceCollection AddGatewayObservability(
        this IServiceCollection services, IConfiguration config)
    {
        var svcName = config["OpenTelemetry:ServiceName"] ?? "SS.APIGateway";
        var endpoint = config["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(svcName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(opts =>
                {
                    // Never record Authorization header (PII/token protection)
                    opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    opts.EnrichWithHttpRequest = (activity, req) =>
                    {
                        activity.SetTag("http.client_ip", req.HttpContext.Connection.RemoteIpAddress?.ToString());
                        activity.SetTag("correlation_id", req.Headers["X-Correlation-Id"].FirstOrDefault());
                    };
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(endpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(endpoint)));

        // Structured JSON logging with OTEL
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otelLogging =>
            {
                otelLogging.IncludeFormattedMessage = true;
                otelLogging.IncludeScopes = true;
                otelLogging.ParseStateValues = true;
                otelLogging.AddOtlpExporter(o => o.Endpoint = new Uri(endpoint));
            });
        });

        return services;
    }
}
