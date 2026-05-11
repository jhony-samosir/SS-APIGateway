using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SS.APIGateway.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SS.APIGateway.Transforms;

/// <summary>
/// Signs outgoing requests with an HMAC-SHA256 signature.
/// Downstream services verify this header to ensure requests originated from the Gateway.
/// Secret is injected via environment variable / K8s Secret.
/// 
/// Signature = HMAC-SHA256(secret, "{method}:{path}:{timestamp}")
/// Downstream verifies: recompute signature, check timestamp within ±30s window.
/// </summary>
public sealed class InternalOriginSignatureTransform : ITransformProvider
{
    private readonly InternalSignatureOptions _opts;
    private readonly byte[] _keyBytes;

    public InternalOriginSignatureTransform(IOptions<InternalSignatureOptions> opts)
    {
        _opts = opts.Value;
        var secret = Environment.GetEnvironmentVariable(_opts.SecretKeyEnvVar);
        
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"HMAC secret '{_opts.SecretKeyEnvVar}' is not configured. This is required for zero-trust internal origin verification.");
        }

        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformCtx =>
        {
            var req = transformCtx.ProxyRequest;
            var method = req.Method.Method;
            var path = transformCtx.HttpContext.Request.Path + transformCtx.HttpContext.Request.QueryString;

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var payload = $"{method}:{path}:{ts}";
            var signature = ComputeHmac(payload);

            req.Headers.TryAddWithoutValidation(_opts.HeaderName, signature);
            req.Headers.TryAddWithoutValidation("X-Gateway-Timestamp", ts);

            return ValueTask.CompletedTask;
        });
    }

    private string ComputeHmac(string payload)
    {
        // Use stackalloc for small payloads to avoid heap allocation
        int byteCount = Encoding.UTF8.GetByteCount(payload);
        Span<byte> payloadBytes = byteCount <= 1024 ? stackalloc byte[byteCount] : new byte[byteCount];
        Encoding.UTF8.GetBytes(payload, payloadBytes);

        Span<byte> hash = stackalloc byte[32]; // SHA256 hash size is 32 bytes
        HMACSHA256.HashData(_keyBytes, payloadBytes, hash);

        return Convert.ToBase64String(hash);
    }

}
