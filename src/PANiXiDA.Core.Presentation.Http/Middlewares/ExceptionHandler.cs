using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class ExceptionHandler(
    ILogger<ExceptionHandler> logger,
    IHostEnvironment hostEnvironment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        var endpoint = httpContext.GetEndpoint();

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext.User.Identity?.Name;

        logger.LogError(
            exception,
            """
            Unhandled HTTP exception.
            TraceIdentifier: {TraceIdentifier}
            TraceId: {TraceId}
            SpanId: {SpanId}
            Method: {Method}
            Path: {Path}
            QueryString: {QueryString}
            Endpoint: {Endpoint}
            UserId: {UserId}
            UserName: {UserName}
            RemoteIp: {RemoteIp}
            UserAgent: {UserAgent}
            """,
            httpContext.TraceIdentifier,
            activity?.TraceId.ToString(),
            activity?.SpanId.ToString(),
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            httpContext.Request.QueryString.Value,
            endpoint?.DisplayName,
            userId,
            userName,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal server error",
            Detail = hostEnvironment.IsDevelopment() ? exception.Message : null,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["activityTraceId"] = activity?.TraceId.ToString()
            }
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await Results.Problem(problemDetails).ExecuteAsync(httpContext);

        return true;
    }
}
