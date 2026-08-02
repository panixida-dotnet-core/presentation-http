using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Diagnostics;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class BadHttpRequestExceptionHandlerTests
{
    [Fact(DisplayName = "TryHandleAsync returns ProblemDetails for a bad HTTP request")]
    public async Task TryHandleAsync_ShouldWriteProblemDetailsForBadHttpRequest()
    {
        using var activity = new Activity("bad-http-request").Start();

        var logger = new TestLogger<BadHttpRequestExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateHttpContext(serviceProvider);
        var exception = new BadHttpRequestException(
            "Failed to read the request body.",
            StatusCodes.Status400BadRequest);
        var handler = new BadHttpRequestExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(
            httpContext,
            exception,
            CancellationToken.None);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        using var document = ReadResponseBody(httpContext);
        var root = document.RootElement;
        root.GetProperty("title").GetString().ShouldBe("Bad Request");
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status400BadRequest);
        root.GetProperty("detail").GetString().ShouldBe("Failed to read the request body.");
        root.GetProperty("traceId").GetString().ShouldBe(activity.Id);
        root.GetProperty("activityTraceId").GetString().ShouldBe(activity.TraceId.ToString());

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.LogLevel.ShouldBe(LogLevel.Warning);
        logEntry.Exception.ShouldBeSameAs(exception);
        logEntry.Message.ShouldBe("Invalid HTTP request");

        var scopeValues = logger.Scopes
            .ShouldHaveSingleItem()
            .ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;

        scopeValues["network.protocol.name"].ShouldBe("http");
        scopeValues["http.request.method"].ShouldBe(HttpMethods.Post);
        scopeValues["url.path"].ShouldBe("/orders");
        scopeValues["url.query"].ShouldBe(string.Empty);
        scopeValues["http.route"].ShouldBe("/orders");
        scopeValues["aspnetcore.endpoint.display_name"].ShouldBe("Test endpoint");
        scopeValues["enduser.id"].ShouldBe("user-id");
        scopeValues["client.address"].ShouldBe("127.0.0.1");
        scopeValues["user_agent.original"].ShouldBe("UnitTest");
    }

    [Fact(DisplayName = "TryHandleAsync ignores exceptions that are not bad HTTP requests")]
    public async Task TryHandleAsync_ShouldIgnoreOtherExceptions()
    {
        var logger = new TestLogger<BadHttpRequestExceptionHandler>();
        var environment = new TestHostEnvironment();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();
        var handler = new BadHttpRequestExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Unexpected failure"),
            CancellationToken.None);

        handled.ShouldBeFalse();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        httpContext.Response.Body.Length.ShouldBe(0);
        logger.Entries.ShouldBeEmpty();
        logger.Scopes.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TryHandleAsync hides bad request details outside Development")]
    public async Task TryHandleAsync_ShouldHideBadRequestDetailsOutsideDevelopment()
    {
        var logger = new TestLogger<BadHttpRequestExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext(serviceProvider);
        var handler = new BadHttpRequestExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new BadHttpRequestException("Sensitive parser details"),
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
