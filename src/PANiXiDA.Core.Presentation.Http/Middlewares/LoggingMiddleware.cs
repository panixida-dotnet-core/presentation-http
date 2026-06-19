using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class LoggingMiddleware(
    RequestDelegate next,
    ILogger<LoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var endpoint = httpContext.GetEndpoint();
        var route = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["network.protocol.name"] = "http",
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["http.route"] = route,
            ["aspnetcore.endpoint.display_name"] = endpoint?.DisplayName,
            ["enduser.id"] = userId,
            ["client.address"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["user_agent.original"] = httpContext.Request.Headers.UserAgent.ToString(),
        }))
        {
            try
            {
                await next(httpContext);
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(startedAt);
                var logLevel = GetLogLevel(httpContext.Response.StatusCode);

                if (logger.IsEnabled(logLevel))
                {
                    using (logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["http.response.status_code"] = httpContext.Response.StatusCode,
                        ["http.server.request.duration_ms"] = elapsed.TotalMilliseconds,
                    }))
                    {
                        logger.Log(logLevel, "HTTP request finished");
                    }
                }
            }
        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogLevel.Error;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            return LogLevel.Warning;
        }

        return LogLevel.Information;
    }
}
