using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Diagnostics;

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
        using var activity = new Activity("http-request").Start();

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
        logEntry.Message.ShouldStartWith($"HTTP request finished with status code {statusCode}");

        var scope = logger.Scopes.ShouldHaveSingleItem();
        var scopeValues = scope.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        scopeValues["Transport"].ShouldBe("http");
        scopeValues["TraceIdentifier"].ShouldBe(httpContext.TraceIdentifier);
        scopeValues["TraceId"].ShouldBe(activity.TraceId.ToString());
        scopeValues["SpanId"].ShouldBe(activity.SpanId.ToString());
        scopeValues["Method"].ShouldBe(HttpMethods.Post);
        scopeValues["Path"].ShouldBe("/orders");
        scopeValues["Endpoint"].ShouldBe("Test endpoint");
        scopeValues["UserId"].ShouldBe("user-id");
        scopeValues["UserName"].ShouldBe("user-name");
        scopeValues["RemoteIp"].ShouldBe("127.0.0.1");
        scopeValues["UserAgent"].ShouldBe("UnitTest");
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

        var act = async () => await middleware.InvokeAsync(httpContext);

        var thrownException = await Should.ThrowAsync<InvalidOperationException>(act);

        thrownException.Message.ShouldBe("Request failed");
        logger.Entries.ShouldHaveSingleItem().LogLevel.ShouldBe(LogLevel.Information);
    }

    [Fact(DisplayName = "InvokeAsync supports requests without optional context")]
    public async Task InvokeAsync_ShouldSupportRequestWithoutOptionalContext()
    {
        Activity.Current = null;

        var logger = new TestLogger<LoggingMiddleware>();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();

        static Task next(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;

            return Task.CompletedTask;
        }

        var middleware = new LoggingMiddleware(next, logger);

        await middleware.InvokeAsync(httpContext);

        var scope = logger.Scopes.ShouldHaveSingleItem();
        var scopeValues = scope.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        scopeValues["TraceId"].ShouldBeNull();
        scopeValues["SpanId"].ShouldBeNull();
        scopeValues["Endpoint"].ShouldBeNull();
        scopeValues["UserId"].ShouldBeNull();
        scopeValues["UserName"].ShouldBeNull();
        scopeValues["RemoteIp"].ShouldBeNull();
        scopeValues["UserAgent"].ShouldBe(string.Empty);
    }
}
