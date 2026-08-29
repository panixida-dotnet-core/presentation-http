using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class LoggingMiddlewareTests
{
    [Theory(DisplayName = "InvokeAsync writes the expected log level by response status")]
    [InlineData(StatusCodes.Status204NoContent, LogLevel.Information)]
    [InlineData(StatusCodes.Status404NotFound, LogLevel.Warning)]
    [InlineData(StatusCodes.Status500InternalServerError, LogLevel.Error)]
    public async Task InvokeAsync_ShouldWriteExpectedLogLevelByStatusCode(
        int statusCode,
        LogLevel expectedLogLevel)
    {
        var logger = new TestLogger<LoggingMiddleware>();
        var httpContext = TestHttpContextFactory.CreateHttpContext();
        Task next(HttpContext context)
        {
            context.Response.StatusCode = statusCode;

            return Task.CompletedTask;
        }

        var middleware = new LoggingMiddleware(next, logger);

        await middleware.InvokeAsync(httpContext);

        var logEntry = logger.Entries.ShouldHaveSingleItem();
        logEntry.LogLevel.ShouldBe(expectedLogLevel);
        logEntry.Message.ShouldBe("HTTP request finished");

        var requestScope = FindScope(logger, "http.request.method");
        var responseScope = FindScope(logger, "http.response.status_code");

        var scopeValues = requestScope;
        scopeValues["network.protocol.name"].ShouldBe("http");
        scopeValues["http.request.method"].ShouldBe(HttpMethods.Post);
        scopeValues["url.path"].ShouldBe("/orders");
        scopeValues["url.query"].ShouldBe(string.Empty);
        scopeValues["http.route"].ShouldBe("/orders");
        scopeValues["aspnetcore.endpoint.display_name"].ShouldBe("Test endpoint");
        scopeValues["enduser.id"].ShouldBe("user-id");
        scopeValues["client.address"].ShouldBe("127.0.0.1");
        scopeValues["user_agent.original"].ShouldBe("UnitTest");
        scopeValues.ContainsKey("TraceIdentifier").ShouldBeFalse();
        scopeValues.ContainsKey("TraceId").ShouldBeFalse();
        scopeValues.ContainsKey("SpanId").ShouldBeFalse();

        responseScope["http.response.status_code"].ShouldBe(statusCode);
        responseScope["http.server.request.duration_ms"].ShouldBeAssignableTo<double>();
    }

    [Fact(DisplayName = "InvokeAsync logs request completion when the next middleware throws")]
    public async Task InvokeAsync_ShouldLogRequestCompletionWhenNextMiddlewareThrows()
    {
        var logger = new TestLogger<LoggingMiddleware>();
        var httpContext = TestHttpContextFactory.CreateHttpContext();
        var exception = new InvalidOperationException("Request failed");

        Task next(HttpContext _)
        {
            throw exception;
        }

        var middleware = new LoggingMiddleware(next, logger);

        async Task act() => await middleware.InvokeAsync(httpContext);

        var thrownException = await Should.ThrowAsync<InvalidOperationException>(act);

        thrownException.Message.ShouldBe("Request failed");
        logger.Entries.ShouldHaveSingleItem().LogLevel.ShouldBe(LogLevel.Information);
    }

    [Fact(DisplayName = "InvokeAsync supports requests without optional context")]
    public async Task InvokeAsync_ShouldSupportRequestWithoutOptionalContext()
    {
        var logger = new TestLogger<LoggingMiddleware>();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();

        static Task next(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;

            return Task.CompletedTask;
        }

        var middleware = new LoggingMiddleware(next, logger);

        await middleware.InvokeAsync(httpContext);

        var scopeValues = FindScope(logger, "http.request.method");

        scopeValues["http.route"].ShouldBeNull();
        scopeValues["aspnetcore.endpoint.display_name"].ShouldBeNull();
        scopeValues["enduser.id"].ShouldBeNull();
        scopeValues["client.address"].ShouldBeNull();
        scopeValues["user_agent.original"].ShouldBe(string.Empty);
        scopeValues.ContainsKey("TraceId").ShouldBeFalse();
        scopeValues.ContainsKey("SpanId").ShouldBeFalse();
    }

    private static IReadOnlyDictionary<string, object?> FindScope(
        TestLogger<LoggingMiddleware> logger,
        string key)
    {
        return logger.Scopes
            .Select(scope => scope.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!)
            .Single(scope => scope.ContainsKey(key));
    }
}
