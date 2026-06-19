using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
        var route = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["url.query"] = httpContext.Request.QueryString.Value,
            ["http.route"] = route,
            ["aspnetcore.endpoint.display_name"] = endpoint?.DisplayName,
            ["enduser.id"] = userId,
            ["client.address"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["user_agent.original"] = httpContext.Request.Headers.UserAgent.ToString(),
        }))
        {
            logger.LogError(exception, "Unhandled HTTP exception");
        }

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
