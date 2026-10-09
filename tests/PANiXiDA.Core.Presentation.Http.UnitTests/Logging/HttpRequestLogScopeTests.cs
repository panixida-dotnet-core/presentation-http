using Microsoft.AspNetCore.Http;

using System.Security.Claims;
using System.Collections;

using PANiXiDA.Core.Presentation.Http.Logging;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Logging;

public sealed class HttpRequestLogScopeTests
{
    [Theory(DisplayName = "Create uses the authenticated subject with a NameIdentifier fallback")]
    [InlineData("subject-id", "mapped-id", "subject-id")]
    [InlineData("subject-id", null, "subject-id")]
    [InlineData(null, "mapped-id", "mapped-id")]
    [InlineData(null, null, null)]
    public void Create_ShouldResolveUserId(string? subject, string? nameIdentifier, string? expected)
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

        var scope = HttpRequestLogScope.Create(context);

        scope["enduser.id"].ShouldBe(expected);
        scope.Single(pair => pair.Key == "enduser.id").Value.ShouldBe(expected);
    }

    [Theory(DisplayName = "Request scope ignores claims from unauthenticated identities")]
    [InlineData(false, false, null)]
    [InlineData(true, false, "authenticated-user")]
    [InlineData(true, true, "authenticated-user")]
    public void Create_ShouldIgnoreUnauthenticatedIdentityClaims(bool includeAuthenticatedIdentity, bool useSubject, string? expected)
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
        var scope = HttpRequestLogScope.Create(context);

        scope["enduser.id"].ShouldBe(expected);
        scope.Complete();
        context.User = new ClaimsPrincipal();
        scope["enduser.id"].ShouldBe(expected);
    }

    [Fact(DisplayName = "Request scope observes authentication after scope creation")]
    public void Create_ShouldObserveAuthenticationAfterScopeCreation()
    {
        var context = new DefaultHttpContext();
        var scope = HttpRequestLogScope.Create(context);
        scope["enduser.id"].ShouldBeNull();

        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "authenticated-user")], "test"));

        scope["enduser.id"].ShouldBe("authenticated-user");
        scope.Single(pair => pair.Key == "enduser.id").Value.ShouldBe("authenticated-user");
    }

    [Fact(DisplayName = "Completed request scope retains the user after the HTTP context is reused")]
    public void Complete_ShouldRetainUserAfterHttpContextChanges()
    {
        var context = TestHttpContextFactory.CreateHttpContext();
        var scope = HttpRequestLogScope.Create(context);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "authenticated-user")], "test"));

        scope.Complete();
        context.User = new ClaimsPrincipal();
        scope.Complete();

        scope["enduser.id"].ShouldBe("authenticated-user");
        scope.TryGetValue("enduser.id", out var userId).ShouldBeTrue();
        userId.ShouldBe("authenticated-user");
        scope.Single(pair => pair.Key == "enduser.id").Value.ShouldBe("authenticated-user");
        scope.Values.ShouldContain("authenticated-user");
        var untypedAttributes = new List<KeyValuePair<string, object?>>();
        foreach (KeyValuePair<string, object?> attribute in (IEnumerable)scope)
        {
            untypedAttributes.Add(attribute);
        }

        untypedAttributes.ToArray().ShouldBe(scope.ToArray());
        scope.ContainsKey("missing").ShouldBeFalse();
        scope.TryGetValue("missing", out _).ShouldBeFalse();
        scope.TryGetValue("http.request.method", out var method).ShouldBeTrue();
        method.ShouldBe(HttpMethods.Post);
    }

    [Fact(DisplayName = "Create returns the centralized HTTP request log attributes")]
    public void Create_ShouldReturnHttpRequestLogAttributes()
    {
        var httpContext = TestHttpContextFactory.CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?status=active");

        var scope = HttpRequestLogScope.Create(httpContext);

        scope.Count.ShouldBe(9);
        scope["network.protocol.name"].ShouldBe("http");
        scope["http.request.method"].ShouldBe(HttpMethods.Post);
        scope["url.path"].ShouldBe("/orders");
        scope["url.query"].ShouldBe("?status=active");
        scope["http.route"].ShouldBe("/orders");
        scope["aspnetcore.endpoint.display_name"].ShouldBe("Test endpoint");
        scope["enduser.id"].ShouldBe("user-id");
        scope["client.address"].ShouldBe("127.0.0.1");
        scope["user_agent.original"].ShouldBe("UnitTest");
        scope.ContainsKey("TraceIdentifier").ShouldBeFalse();
        scope.ContainsKey("TraceId").ShouldBeFalse();
        scope.ContainsKey("SpanId").ShouldBeFalse();
    }

    [Fact(DisplayName = "Create rejects a null HTTP context")]
    public void Create_ShouldRejectNullHttpContext()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            HttpRequestLogScope.Create(null!);
        });

        exception.ParamName.ShouldBe("httpContext");
    }
}
