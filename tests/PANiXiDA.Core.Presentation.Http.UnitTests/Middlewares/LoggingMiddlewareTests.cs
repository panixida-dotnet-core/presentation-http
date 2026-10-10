using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class LoggingMiddlewareTests
{
    [Theory(DisplayName = "InvokeAsync writes the expected log level by response status")]
    [InlineData(StatusCodes.Status204NoContent, LogLevel.Information)]
    [InlineData(StatusCodes.Status404NotFound, LogLevel.Warning)]
    [InlineData(StatusCodes.Status499ClientClosedRequest, LogLevel.Warning)]
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
        logEntry.Exception.ShouldBeNull();

        var scopeValues = GetScopeAttributes(logger);
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

        scopeValues["http.response.status_code"].ShouldBe(statusCode);
        scopeValues["http.server.request.duration_ms"].ShouldBeAssignableTo<double>();
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

        var scopeValues = GetScopeAttributes(logger);

        scopeValues["http.route"].ShouldBeNull();
        scopeValues["aspnetcore.endpoint.display_name"].ShouldBeNull();
        scopeValues["enduser.id"].ShouldBeNull();
        scopeValues["client.address"].ShouldBeNull();
        scopeValues["user_agent.original"].ShouldBe(string.Empty);
        scopeValues.ContainsKey("TraceId").ShouldBeFalse();
        scopeValues.ContainsKey("SpanId").ShouldBeFalse();
    }

    [Theory(DisplayName = "InvokeAsync retains the authenticated user after the request context changes")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeAsync_ShouldRetainAuthenticatedUserWhenRequestContextChanges(bool throwException)
    {
        var logger = new TestLogger<LoggingMiddleware>();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();
        Task next(HttpContext context)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "authenticated-user")], "test"));
            return throwException
                ? Task.FromException(new InvalidOperationException("Request failed"))
                : Task.CompletedTask;
        }

        var middleware = new LoggingMiddleware(next, logger);

        if (throwException)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
        }
        else
        {
            await middleware.InvokeAsync(httpContext);
        }

        httpContext.User = new ClaimsPrincipal();
        var scopeValues = GetScopeAttributes(logger);
        scopeValues["enduser.id"].ShouldBe("authenticated-user");
    }

    private static Dictionary<string, object?> GetScopeAttributes(TestLogger<LoggingMiddleware> logger)
    {
        return logger.Scopes
            .SelectMany(scope => scope.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!)
            .ToDictionary();
    }
}
