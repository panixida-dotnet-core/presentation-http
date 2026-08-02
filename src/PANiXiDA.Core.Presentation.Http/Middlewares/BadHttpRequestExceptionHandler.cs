using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Logging;

using System.Diagnostics;

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

        using (logger.BeginScope(HttpRequestLogScope.Create(httpContext)))
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
