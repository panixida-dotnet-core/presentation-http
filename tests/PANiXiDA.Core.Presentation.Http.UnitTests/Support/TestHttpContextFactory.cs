using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

using System.Net;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Support;

internal static class TestHttpContextFactory
{
    internal static DefaultHttpContext CreateHttpContext(IServiceProvider? requestServices = null)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-id",
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-id"),
                new Claim(ClaimTypes.Name, "user-name")
            ], "TestAuthentication"))
        };

        if (requestServices is not null)
        {
            httpContext.RequestServices = requestServices;
        }

        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/orders";
        httpContext.Request.Headers.UserAgent = "UnitTest";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        httpContext.SetEndpoint(new RouteEndpoint(
            static context => Task.CompletedTask,
            RoutePatternFactory.Parse("/orders"),
            order: 0,
            new EndpointMetadataCollection(),
            "Test endpoint"));

        return httpContext;
    }

    internal static DefaultHttpContext CreateMinimalHttpContext(IServiceProvider? requestServices = null)
    {
        var httpContext = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            },
            User = new ClaimsPrincipal()
        };

        if (requestServices is not null)
        {
            httpContext.RequestServices = requestServices;
        }

        return httpContext;
    }
}
