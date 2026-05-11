using Microsoft.Extensions.Http.Resilience;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Polly.CircuitBreaker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SS.APIGateway.Extensions;

public static class ResiliencyExtensions
{
    /// <summary>
    /// Adds Polly-based resilience: Timeout, Retry (with exponential backoff), Circuit Breaker.
    /// Integrated with YARP via a custom IForwarderHttpClientFactory.
    /// </summary>
    public static IReverseProxyBuilder AddResiliencyPolicies(
        this IReverseProxyBuilder proxyBuilder, IConfiguration config)
    {
        // 1. Register a ResiliencePipelineRegistry that we can dynamically populate per cluster

        proxyBuilder.Services.AddSingleton<ResiliencePipelineRegistry<string>>(sp => 
        {
            var registry = new ResiliencePipelineRegistry<string>();
            return registry;
        });

        // 2. Register custom factory to apply the pipeline
        proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory, ResilienceForwarderHttpClientFactory>(sp => 
        {
            var logger = sp.GetRequiredService<ILogger<ForwarderHttpClientFactory>>();
            var registry = sp.GetRequiredService<ResiliencePipelineRegistry<string>>();
            var config = sp.GetRequiredService<IConfiguration>();
            return new ResilienceForwarderHttpClientFactory(logger, registry, config);
        });

        return proxyBuilder;
    }
}

/// <summary>
/// Custom YARP HTTP Client Factory that injects a Polly ResilienceHandler.
/// </summary>
internal sealed class ResilienceForwarderHttpClientFactory(
    ILogger<ForwarderHttpClientFactory> logger, 
    ResiliencePipelineRegistry<string> registry,
    IConfiguration config) 
    : ForwarderHttpClientFactory(logger)
{
    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        var baseHandler = base.WrapHandler(context, handler);
        
        var timeoutSec = config.GetValue("Resilience:TimeoutSeconds", 30);
        var retryCount = config.GetValue("Resilience:RetryCount", 3);
        var cbRatio = config.GetValue("Resilience:CircuitBreakerFailureRatio", 0.5);
        var cbSampling = config.GetValue("Resilience:CircuitBreakerSamplingDuration", TimeSpan.FromSeconds(30));

        // Get or build a pipeline specifically for this cluster
        var pipeline = registry.GetOrAddPipeline<HttpResponseMessage>($"cluster-{context.ClusterId}", builder =>
        {
            // 1. Timeout
            builder.AddTimeout(TimeSpan.FromSeconds(timeoutSec));

            // 2. Retry — exponential backoff, only on transient errors AND idempotent methods
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = retryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => 
                {
                    var isTransient = args.Outcome.Exception is HttpRequestException || 
                                     (int?)args.Outcome.Result?.StatusCode >= 500;
                    
                    if (!isTransient) return ValueTask.FromResult(false);

                    // Idempotency check: only retry GET, PUT, DELETE, HEAD, OPTIONS
                    var method = args.Context.Properties.GetValue(new ResiliencePropertyKey<string>("RequestMethod"), "UNKNOWN");
                    var isIdempotent = method is "GET" or "PUT" or "DELETE" or "HEAD" or "OPTIONS";

                    return ValueTask.FromResult(isIdempotent);
                }
            });

            // 3. Circuit Breaker - isolated per cluster
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = cbRatio,
                SamplingDuration = cbSampling,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30)
            });
        });
        
        return new ResilienceHandler(pipeline) 
        { 
            InnerHandler = baseHandler 
        };
    }
}

/// <summary>
/// Helper handler that wraps a ResiliencePipeline around an InnerHandler.
/// </summary>
internal sealed class ResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Inject request method into properties for retry logic
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(new ResiliencePropertyKey<string>("RequestMethod"), request.Method.Method.ToUpperInvariant());

        try
        {
            return await pipeline.ExecuteAsync(async (ctx, token) => 
                await base.SendAsync(request, token), context);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }
}
