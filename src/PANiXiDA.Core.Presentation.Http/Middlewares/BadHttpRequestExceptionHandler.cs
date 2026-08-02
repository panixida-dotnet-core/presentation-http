using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class BadHttpRequestExceptionHandler(
    ILogger<BadHttpRequestExceptionHandler> logger,
    IHostEnvironment hostEnvironment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badHttpRequestException)
        {
            return false;
        }

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
            logger.LogWarning(exception, "Invalid HTTP request");
        }

        var statusCode = badHttpRequestException.StatusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = hostEnvironment.IsDevelopment() ? exception.Message : null,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["activityTraceId"] = activity?.TraceId.ToString()
            }
        };

        httpContext.Response.StatusCode = statusCode;

        await Results.Problem(problemDetails).ExecuteAsync(httpContext);

        return true;
    }
}
