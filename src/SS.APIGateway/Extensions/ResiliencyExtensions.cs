using Microsoft.Extensions.Http.Resilience;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Polly;
using Polly.Registry;
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
        var timeoutSec = config.GetValue("Resilience:TimeoutSeconds", 30);
        var retryCount = config.GetValue("Resilience:RetryCount", 3);
        var cbRatio = config.GetValue("Resilience:CircuitBreakerFailureRatio", 0.5);
        var cbSampling = config.GetValue("Resilience:CircuitBreakerSamplingDuration", TimeSpan.FromSeconds(30));

        // 1. Register the resilience pipeline
        proxyBuilder.Services.AddResiliencePipeline<string, HttpResponseMessage>("gateway-pipeline", pipeline =>
        {
            // 1. Timeout
            pipeline.AddTimeout(TimeSpan.FromSeconds(timeoutSec));

            // 2. Retry — exponential backoff, only on transient errors AND idempotent methods
            pipeline.AddRetry(new HttpRetryStrategyOptions
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

            // 3. Circuit Breaker
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = cbRatio,
                SamplingDuration = cbSampling,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30)
            });
        });

        // 2. Register custom factory to apply the pipeline
        proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory, ResilienceForwarderHttpClientFactory>();

        return proxyBuilder;
    }
}

/// <summary>
/// Custom YARP HTTP Client Factory that injects a Polly ResilienceHandler.
/// </summary>
internal sealed class ResilienceForwarderHttpClientFactory(
    ILogger<ForwarderHttpClientFactory> logger, 
    ResiliencePipelineProvider<string> pipelineProvider) 
    : ForwarderHttpClientFactory(logger)
{
    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        var baseHandler = base.WrapHandler(context, handler);
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>("gateway-pipeline");
        
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
