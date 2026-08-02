using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.Errors;
using PANiXiDA.Core.Presentation.Http.Logging;

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

        using (logger.BeginScope(HttpRequestLogScope.Create(httpContext)))
        {
            logger.LogWarning(exception, "Invalid HTTP request");
        }

        var statusCode = badHttpRequestException.StatusCode;
        var problemDetails = ExceptionProblemDetailsFactory.Create(
            httpContext,
            exception,
            hostEnvironment,
            statusCode,
            ReasonPhrases.GetReasonPhrase(statusCode));

        httpContext.Response.StatusCode = statusCode;

        await Results.Problem(problemDetails).ExecuteAsync(httpContext);

        return true;
    }
}
