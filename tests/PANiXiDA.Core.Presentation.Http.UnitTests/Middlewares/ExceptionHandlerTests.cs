using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Diagnostics;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class ExceptionHandlerTests
{
    [Fact(DisplayName = "TryHandleAsync returns ProblemDetails with exception details in Development")]
    public async Task TryHandleAsync_ShouldWriteProblemDetailsWithExceptionMessageInDevelopment()
    {
        using var activity = new Activity("http-exception").Start();

        var logger = new TestLogger<ExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateHttpContext(serviceProvider);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.QueryString = new QueryString("?status=active");

        var exception = new InvalidOperationException("Development failure");
        var handler = new ExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        using var document = ReadResponseBody(httpContext);
        var root = document.RootElement;
        root.GetProperty("title").GetString().ShouldBe("Internal server error");
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status500InternalServerError);
        root.GetProperty("detail").GetString().ShouldBe("Development failure");
        root.GetProperty("traceId").GetString().ShouldBe(activity.Id);
        root.GetProperty("activityTraceId").GetString().ShouldBe(activity.TraceId.ToString());

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.LogLevel.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Error);
        logEntry.Exception.ShouldBeSameAs(exception);
        logEntry.Message.ShouldBe("Unhandled HTTP exception");

        var scopeValues = logger.Scopes
            .ShouldHaveSingleItem()
            .ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;

        scopeValues["network.protocol.name"].ShouldBe("http");
        scopeValues["http.request.method"].ShouldBe(HttpMethods.Get);
        scopeValues["url.path"].ShouldBe("/orders");
        scopeValues["url.query"].ShouldBe("?status=active");
        scopeValues["http.route"].ShouldBe("/orders");
        scopeValues["aspnetcore.endpoint.display_name"].ShouldBe("Test endpoint");
        scopeValues["enduser.id"].ShouldBe("user-id");
        scopeValues["client.address"].ShouldBe("127.0.0.1");
        scopeValues["user_agent.original"].ShouldBe("UnitTest");
        scopeValues.ContainsKey("TraceIdentifier").ShouldBeFalse();
        scopeValues.ContainsKey("TraceId").ShouldBeFalse();
        scopeValues.ContainsKey("SpanId").ShouldBeFalse();
    }

    [Fact(DisplayName = "TryHandleAsync hides exception details outside Development")]
    public async Task TryHandleAsync_ShouldHideExceptionMessageOutsideDevelopment()
    {
        Activity.Current = null;

        var logger = new TestLogger<ExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext(serviceProvider);
        var handler = new ExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Production failure"),
            CancellationToken.None);

        handled.ShouldBeTrue();

        using var document = ReadResponseBody(httpContext);
        document.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();
    }

    private static ServiceProvider CreateRequestServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        return services.BuildServiceProvider();
    }

    private static JsonDocument ReadResponseBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        return JsonDocument.Parse(httpContext.Response.Body);
    }

}
