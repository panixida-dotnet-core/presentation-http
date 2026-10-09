using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal sealed class ClientAbortedExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.RequestAborted.IsCancellationRequested
            || !CancellationExceptionDetector.IsCancellation(exception))
        {
            return ValueTask.FromResult(false);
        }

        if (!httpContext.Response.HasStarted)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }

        return ValueTask.FromResult(true);
    }
}
