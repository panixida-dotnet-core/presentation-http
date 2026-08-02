using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Errors;
using PANiXiDA.Core.Presentation.Http.Logging;

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
        using (logger.BeginScope(HttpRequestLogScope.Create(httpContext)))
        {
            logger.LogError(exception, "Unhandled HTTP exception");
        }

        var problemDetails = ExceptionProblemDetailsFactory.Create(
            httpContext,
            exception,
            hostEnvironment,
            StatusCodes.Status500InternalServerError,
            "Internal server error");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await Results.Problem(problemDetails).ExecuteAsync(httpContext);

        return true;
    }
}
