using Microsoft.Extensions.Options;
using SS.APIGateway.Configuration;
using SS.APIGateway.Transforms;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SS.APIGateway.Tests;

public class InternalOriginSignatureTransformTests
{
    [Fact]
    public void Apply_AddsRequestTransform()
    {
        // Arrange
        var opts = Options.Create(new InternalSignatureOptions
        {
            HeaderName = "X-Internal-Signature",
            SecretKeyEnvVar = "GATEWAY_HMAC_SECRET"
        });
        
        // Ensure env var is set for test
        Environment.SetEnvironmentVariable("GATEWAY_HMAC_SECRET", "test_secret");

        var provider = new InternalOriginSignatureTransform(opts);
        var context = new TransformBuilderContext();

        // Act
        provider.Apply(context);

        // Assert
        Assert.NotEmpty(context.RequestTransforms);
    }
}
