using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using SS.APIGateway.Middleware;
using Yarp.ReverseProxy.Model;


namespace SS.APIGateway.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenMissing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CorrelationIdMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var middleware = new CorrelationIdMiddleware(nextMock.Object, loggerMock.Object);
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Request.Headers.ContainsKey("X-Correlation-Id"));
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-Id"));
    }

    [Fact]
    public async Task InvokeAsync_ValidatesClientCorrelationId()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CorrelationIdMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var middleware = new CorrelationIdMiddleware(nextMock.Object, loggerMock.Object);
        var context = new DefaultHttpContext();
        var invalidId = "not-a-guid";
        context.Request.Headers["X-Correlation-Id"] = invalidId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var resultId = context.Request.Headers["X-Correlation-Id"].ToString();
        Assert.NotEqual(invalidId, resultId);
        Assert.True(Guid.TryParse(resultId, out _));
    }
}

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsSecurityHeaders_ToNonProxiedRequests()
    {
        // Arrange
        var nextMock = new Mock<RequestDelegate>();
        var middleware = new SecurityHeadersMiddleware(nextMock.Object);
        
        var contextMock = new Mock<HttpContext>();
        var responseMock = new Mock<HttpResponse>();
        var headers = new HeaderDictionary();
        var features = new FeatureCollection();
        
        contextMock.Setup(c => c.Response).Returns(responseMock.Object);
        contextMock.Setup(c => c.Features).Returns(features);
        responseMock.Setup(r => r.Headers).Returns(headers);
        
        Func<Task> onStartingCallback = null;
        responseMock.Setup(r => r.OnStarting(It.IsAny<Func<Task>>()))
                    .Callback<Func<Task>>(callback => onStartingCallback = callback);

        // Act
        await middleware.InvokeAsync(contextMock.Object);
        
        // Assert
        Assert.NotNull(onStartingCallback);
        await onStartingCallback();
        
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.False(headers.ContainsKey("X-XSS-Protection"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", headers["Content-Security-Policy"]);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAddCsp_ToProxiedRequests()
    {
        // Arrange
        var nextMock = new Mock<RequestDelegate>();
        var middleware = new SecurityHeadersMiddleware(nextMock.Object);
        
        var contextMock = new Mock<HttpContext>();
        var responseMock = new Mock<HttpResponse>();
        var headers = new HeaderDictionary();
        var features = new FeatureCollection();
        // Add YARP feature
        features.Set<Yarp.ReverseProxy.Model.IReverseProxyFeature>(new Mock<Yarp.ReverseProxy.Model.IReverseProxyFeature>().Object);
        
        contextMock.Setup(c => c.Response).Returns(responseMock.Object);
        contextMock.Setup(c => c.Features).Returns(features);
        responseMock.Setup(r => r.Headers).Returns(headers);
        
        Func<Task> onStartingCallback = null;
        responseMock.Setup(r => r.OnStarting(It.IsAny<Func<Task>>()))
                    .Callback<Func<Task>>(callback => onStartingCallback = callback);

        // Act
        await middleware.InvokeAsync(contextMock.Object);
        
        // Assert
        Assert.NotNull(onStartingCallback);
        await onStartingCallback();
        
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }


}
