using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Errors;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Diagnostics;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Errors;

public sealed class ExceptionProblemDetailsFactoryTests
{
    [Fact(DisplayName = "Create includes exception and trace details in Development")]
    public void Create_ShouldIncludeExceptionAndTraceDetailsInDevelopment()
    {
        using var activity = new Activity("problem-details").Start();

        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();
        httpContext.TraceIdentifier = "trace-id";
        var exception = new InvalidOperationException("Development failure");
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        var problemDetails = ExceptionProblemDetailsFactory.Create(
            httpContext,
            exception,
            environment,
            StatusCodes.Status400BadRequest,
            "Bad Request");

        problemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Title.ShouldBe("Bad Request");
        problemDetails.Detail.ShouldBe("Development failure");
        problemDetails.Extensions["traceId"].ShouldBe("trace-id");
        problemDetails.Extensions["activityTraceId"].ShouldBe(activity.TraceId.ToString());
    }

    [Fact(DisplayName = "Create hides exception details outside Development")]
    public void Create_ShouldHideExceptionDetailsOutsideDevelopment()
    {
        Activity.Current = null;

        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext();
        var exception = new InvalidOperationException("Production failure");
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        var problemDetails = ExceptionProblemDetailsFactory.Create(
            httpContext,
            exception,
            environment,
            StatusCodes.Status500InternalServerError,
            "Internal server error");

        problemDetails.Detail.ShouldBeNull();
        problemDetails.Extensions["activityTraceId"].ShouldBeNull();
    }
}
