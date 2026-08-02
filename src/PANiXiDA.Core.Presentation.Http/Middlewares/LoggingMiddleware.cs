using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using System.Diagnostics;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class LoggingMiddleware(
    RequestDelegate next,
    ILogger<LoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var startedAt = Stopwatch.GetTimestamp();

        using (logger.BeginScope(HttpRequestLogScope.Create(httpContext)))
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
