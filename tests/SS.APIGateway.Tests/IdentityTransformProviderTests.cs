using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SS.APIGateway.Common;
using SS.APIGateway.Transforms;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SS.APIGateway.Tests;

public class IdentityTransformProviderTests
{
    [Fact]
    public async Task Apply_StripsSpoofableHeaders()
    {
        // Arrange
        var provider = new IdentityTransformProvider();
        var context = new TransformBuilderContext
        {
            Route = new Yarp.ReverseProxy.Configuration.RouteConfig { RouteId = "test" }
        };
        
        // Act
        provider.Apply(context);

        // Assert
        Assert.Contains(context.RequestTransforms, t => t is RequestHeaderRemoveTransform);
    }

    [Fact]
    public void Apply_AddsIdentityInjection_WhenRequiresJwtIsTrue()
    {
        // Arrange
        var provider = new IdentityTransformProvider();
        var context = new TransformBuilderContext
        {
            Route = new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "test",
                Metadata = new Dictionary<string, string> { { "RequiresJwt", "true" } }
            }
        };

        // Act
        provider.Apply(context);

        // Assert
        // We expect at least one request transform to be added for identity injection
        Assert.NotEmpty(context.RequestTransforms);
    }
}
