using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

using System.Security.Claims;

using PANiXiDA.Core.Presentation.Http.Logging;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Logging;

public sealed class HttpRequestLogScopeTests
{
    [Theory(DisplayName = "User scope uses the authenticated subject with a NameIdentifier fallback")]
    [InlineData("subject-id", "mapped-id", "subject-id")]
    [InlineData("subject-id", null, "subject-id")]
    [InlineData(null, "mapped-id", "mapped-id")]
    [InlineData(null, null, null)]
    public void CreateUser_ShouldResolveUserId(string? subject, string? nameIdentifier, string? expected)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim> { new(ClaimTypes.Name, "display-name") };
        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }
        if (nameIdentifier is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var scope = HttpRequestLogScope.CreateUser(context);

        scope.ShouldHaveSingleItem().Key.ShouldBe("enduser.id");
        scope["enduser.id"].ShouldBe(expected);
        context.User = new ClaimsPrincipal();
        scope["enduser.id"].ShouldBe(expected);
    }

    [Theory(DisplayName = "User scope ignores claims from unauthenticated identities")]
    [InlineData(false, false, null)]
    [InlineData(true, false, "authenticated-user")]
    [InlineData(true, true, "authenticated-user")]
    public void CreateUser_ShouldIgnoreUnauthenticatedIdentityClaims(bool includeAuthenticatedIdentity, bool useSubject, string? expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "untrusted-subject"),
            new Claim(ClaimTypes.NameIdentifier, "untrusted-mapped-id")]));
        if (includeAuthenticatedIdentity)
        {
            principal.AddIdentity(new ClaimsIdentity([
                new Claim(useSubject ? "sub" : ClaimTypes.NameIdentifier, "authenticated-user")], "test"));
        }
        var context = new DefaultHttpContext { User = principal };

        var scope = HttpRequestLogScope.CreateUser(context);

        scope["enduser.id"].ShouldBe(expected);
    }

    [Fact(DisplayName = "User scope is a snapshot and does not acquire a later authenticated identity")]
    public void CreateUser_ShouldKeepPreviouslyCapturedIdentity()
    {
        var context = new DefaultHttpContext();
        var beforeAuthentication = HttpRequestLogScope.CreateUser(context);

        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "authenticated-user")], "test"));
        var afterAuthentication = HttpRequestLogScope.CreateUser(context);
        context.User = new ClaimsPrincipal();

        beforeAuthentication["enduser.id"].ShouldBeNull();
        afterAuthentication["enduser.id"].ShouldBe("authenticated-user");
    }

    [Theory(DisplayName = "Endpoint scope snapshots the selected endpoint including handled exceptions")]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateEndpoint_ShouldRetainEndpointMetadata(bool exceptionHandled)
    {
        var context = TestHttpContextFactory.CreateHttpContext();
        var endpoint = context.GetEndpoint();
        if (exceptionHandled)
        {
            context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
            {
                Error = new InvalidOperationException("Request failed"),
                Endpoint = endpoint
            });
            context.SetEndpoint(null);
        }

        var scope = HttpRequestLogScope.CreateEndpoint(context);
        context.SetEndpoint(null);
        context.Features.Set<IExceptionHandlerFeature>(null);

        scope.Count.ShouldBe(2);
        scope["http.route"].ShouldBe("/orders");
        scope["aspnetcore.endpoint.display_name"].ShouldBe("Test endpoint");
    }

    [Theory(DisplayName = "Endpoint scope supports an unmatched request or a non-route endpoint")]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateEndpoint_ShouldSupportMissingRoute(bool hasEndpoint)
    {
        var context = new DefaultHttpContext();
        if (hasEndpoint)
        {
            context.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "Plain endpoint"));
        }

        var scope = HttpRequestLogScope.CreateEndpoint(context);

        scope["http.route"].ShouldBeNull();
        scope["aspnetcore.endpoint.display_name"].ShouldBe(hasEndpoint ? "Plain endpoint" : null);
    }

    [Fact(DisplayName = "Request scope snapshots the base HTTP attributes")]
    public void Create_ShouldReturnHttpRequestLogAttributes()
    {
        var httpContext = TestHttpContextFactory.CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?status=active");

        var scope = HttpRequestLogScope.Create(httpContext);
        httpContext.Request.Path = "/changed";
        httpContext.Request.QueryString = QueryString.Empty;

        scope.Count.ShouldBe(6);
        scope["network.protocol.name"].ShouldBe("http");
        scope["http.request.method"].ShouldBe(HttpMethods.Post);
        scope["url.path"].ShouldBe("/orders");
        scope["url.query"].ShouldBe("?status=active");
        scope["client.address"].ShouldBe("127.0.0.1");
        scope["user_agent.original"].ShouldBe("UnitTest");
        scope.ContainsKey("TraceIdentifier").ShouldBeFalse();
        scope.ContainsKey("TraceId").ShouldBeFalse();
        scope.ContainsKey("SpanId").ShouldBeFalse();
    }

    [Fact(DisplayName = "Request scope rejects a null HTTP context")]
    public void Create_ShouldRejectNullHttpContext()
    {
        Should.Throw<ArgumentNullException>(() => HttpRequestLogScope.Create(null!)).ParamName.ShouldBe("httpContext");
    }

    [Fact(DisplayName = "Endpoint scope rejects a null HTTP context")]
    public void CreateEndpoint_ShouldRejectNullHttpContext()
    {
        Should.Throw<ArgumentNullException>(() => HttpRequestLogScope.CreateEndpoint(null!)).ParamName.ShouldBe("httpContext");
    }

    [Fact(DisplayName = "User scope rejects a null HTTP context")]
    public void CreateUser_ShouldRejectNullHttpContext()
    {
        Should.Throw<ArgumentNullException>(() => HttpRequestLogScope.CreateUser(null!)).ParamName.ShouldBe("httpContext");
    }
}
