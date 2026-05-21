using Microsoft.AspNetCore.Http;
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
        var activity = Activity.Current;
        var endpoint = httpContext.GetEndpoint();

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext.User.Identity?.Name;

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["Transport"] = "http",
            ["TraceIdentifier"] = httpContext.TraceIdentifier,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["SpanId"] = activity?.SpanId.ToString(),
            ["Method"] = httpContext.Request.Method,
            ["Path"] = httpContext.Request.Path.Value,
            ["Endpoint"] = endpoint?.DisplayName,
            ["UserId"] = userId,
            ["UserName"] = userName,
            ["RemoteIp"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["UserAgent"] = httpContext.Request.Headers.UserAgent.ToString(),
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
                    logger.Log(
                        logLevel,
                        "HTTP request finished with status code {StatusCode} in {ElapsedMs} ms",
                        httpContext.Response.StatusCode,
                        elapsed.TotalMilliseconds);
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
