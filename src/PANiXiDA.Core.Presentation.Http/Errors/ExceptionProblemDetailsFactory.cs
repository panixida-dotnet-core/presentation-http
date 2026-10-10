using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

using System.Diagnostics;

namespace PANiXiDA.Core.Presentation.Http.Errors;

internal static class ExceptionProblemDetailsFactory
{
    internal static ProblemDetails Create(
        HttpContext httpContext,
        Exception exception,
        IHostEnvironment hostEnvironment,
        int statusCode,
        string title)
    {
        var activity = Activity.Current;

        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = hostEnvironment.IsDevelopment() ? exception.Message : null,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["activityTraceId"] = activity?.TraceId.ToString()
            }
        };
    }
}
