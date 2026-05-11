using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using SS.APIGateway.Configuration;
using SS.APIGateway.Transforms;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SS.APIGateway.Tests;

public class InternalOriginSignatureTransformTests
{
    [Fact]
    public async Task Apply_AddsRequestTransform_ThatUsesOriginalPath()
    {
        // Arrange
        var headerName = "X-Internal-Signature";
        var secretKey = "test_secret";
        var envVar = "GATEWAY_HMAC_SECRET";
        
        var opts = Options.Create(new InternalSignatureOptions
        {
            HeaderName = headerName,
            SecretKeyEnvVar = envVar
        });
        
        Environment.SetEnvironmentVariable(envVar, secretKey);

        var provider = new InternalOriginSignatureTransform(opts);
        var builderContext = new TransformBuilderContext();

        // Act
        provider.Apply(builderContext);

        // Assert
        Assert.Single(builderContext.RequestTransforms);
        RequestTransform transform = builderContext.RequestTransforms[0];


        // Verify the transform logic
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";
        httpContext.Request.QueryString = new QueryString("?a=1");

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://internal-service/upstream/path");
        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformCtx);



        // Check if headers were added
        Assert.True(proxyRequest.Headers.Contains(headerName));
        Assert.True(proxyRequest.Headers.Contains("X-Gateway-Timestamp"));

        var signature = proxyRequest.Headers.GetValues(headerName).First();
        var timestamp = proxyRequest.Headers.GetValues("X-Gateway-Timestamp").First();

        // Verify signature matches expected payload (method:originalPath:timestamp)
        // Original Path = /api/test?a=1
        var expectedPayload = $"GET:/api/test?a=1:{timestamp}";
        var expectedSignature = ComputeExpectedHmac(secretKey, expectedPayload);

        Assert.Equal(expectedSignature, signature);
    }

    private string ComputeExpectedHmac(string secret, string payload)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var data = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, data);
        return Convert.ToBase64String(hash);
    }
}
