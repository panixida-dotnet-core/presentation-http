using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Errors;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class ExceptionHandler(IHostEnvironment hostEnvironment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
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
