using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Logging;

using System.Diagnostics;

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

        using (logger.BeginScope(HttpRequestLogScope.Create(httpContext)))
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
