using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class HttpRequestLogScopeTests
{
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
