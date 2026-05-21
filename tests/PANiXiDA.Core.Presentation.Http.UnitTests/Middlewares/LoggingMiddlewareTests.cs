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

        var logEntry = logger.Entries.Should().ContainSingle().Subject;
        logEntry.LogLevel.Should().Be(expectedLogLevel);
        logEntry.Message.Should().StartWith($"HTTP request finished with status code {statusCode}");

        var scope = logger.Scopes.Should().ContainSingle().Subject;
        var scopeValues = scope.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        scopeValues["Transport"].Should().Be("http");
        scopeValues["TraceIdentifier"].Should().Be(httpContext.TraceIdentifier);
        scopeValues["TraceId"].Should().Be(activity.TraceId.ToString());
        scopeValues["SpanId"].Should().Be(activity.SpanId.ToString());
        scopeValues["Method"].Should().Be(HttpMethods.Post);
        scopeValues["Path"].Should().Be("/orders");
        scopeValues["Endpoint"].Should().Be("Test endpoint");
        scopeValues["UserId"].Should().Be("user-id");
        scopeValues["UserName"].Should().Be("user-name");
        scopeValues["RemoteIp"].Should().Be("127.0.0.1");
        scopeValues["UserAgent"].Should().Be("UnitTest");
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

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Request failed");
        logger.Entries.Should().ContainSingle().Which.LogLevel.Should().Be(LogLevel.Information);
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

        var scope = logger.Scopes.Should().ContainSingle().Subject;
        var scopeValues = scope.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        scopeValues["TraceId"].Should().BeNull();
        scopeValues["SpanId"].Should().BeNull();
        scopeValues["Endpoint"].Should().BeNull();
        scopeValues["UserId"].Should().BeNull();
        scopeValues["UserName"].Should().BeNull();
        scopeValues["RemoteIp"].Should().BeNull();
        scopeValues["UserAgent"].Should().Be(string.Empty);
    }
}
