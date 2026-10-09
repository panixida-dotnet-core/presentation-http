using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Logging;

using System.Diagnostics;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class LoggingMiddleware(
    RequestDelegate next,
    ILogger<LoggingMiddleware> logger)
{
    internal static void UseEndpointScope(WebApplication app)
    {
        UseScope(app, HttpRequestLogScope.CreateEndpoint);
    }

    internal static void UseUserScope(WebApplication app)
    {
        UseScope(app, HttpRequestLogScope.CreateUser);
    }

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
                    using (logger.BeginScope(HttpRequestLogScope.CreateEndpoint(httpContext)))
                    using (logger.BeginScope(HttpRequestLogScope.CreateUser(httpContext)))
                    using (logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["http.response.status_code"] = httpContext.Response.StatusCode,
                        ["http.server.request.duration_ms"] = elapsed.TotalMilliseconds,
                    }))
                    {
                        logger.Log(
                            logLevel,
                            httpContext.Features.Get<IExceptionHandlerFeature>()?.Error,
                            "HTTP request finished");
                    }
                }
            }
        }
    }

    private static void UseScope(
        WebApplication app,
        Func<HttpContext, IReadOnlyDictionary<string, object?>> createScope)
    {
        app.Use(async (context, next) =>
        {
            using (app.Logger.BeginScope(createScope(context)))
            {
                await next(context);
            }
        });
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
