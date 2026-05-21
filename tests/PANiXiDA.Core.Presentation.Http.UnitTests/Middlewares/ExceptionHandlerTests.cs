using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Diagnostics;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Middlewares;

public sealed class ExceptionHandlerTests
{
    [Fact(DisplayName = "TryHandleAsync returns ProblemDetails with exception details in Development")]
    public async Task TryHandleAsync_ShouldWriteProblemDetailsWithExceptionMessageInDevelopment()
    {
        using var activity = new Activity("http-exception").Start();

        var logger = new TestLogger<ExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateHttpContext(serviceProvider);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.QueryString = new QueryString("?status=active");

        var exception = new InvalidOperationException("Development failure");
        var handler = new ExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        using var document = ReadResponseBody(httpContext);
        var root = document.RootElement;
        root.GetProperty("title").GetString().Should().Be("Internal server error");
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        root.GetProperty("detail").GetString().Should().Be("Development failure");
        root.GetProperty("traceId").GetString().Should().Be(activity.Id);
        root.GetProperty("activityTraceId").GetString().Should().Be(activity.TraceId.ToString());

        var logEntry = logger.Entries.Should().ContainSingle().Subject;
        logEntry.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Error);
        logEntry.Exception.Should().BeSameAs(exception);
    }

    [Fact(DisplayName = "TryHandleAsync hides exception details outside Development")]
    public async Task TryHandleAsync_ShouldHideExceptionMessageOutsideDevelopment()
    {
        Activity.Current = null;

        var logger = new TestLogger<ExceptionHandler>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        using var serviceProvider = CreateRequestServices();
        var httpContext = TestHttpContextFactory.CreateMinimalHttpContext(serviceProvider);
        var handler = new ExceptionHandler(logger, environment);

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("Production failure"),
            CancellationToken.None);

        handled.Should().BeTrue();

        using var document = ReadResponseBody(httpContext);
        document.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
    }

    private static ServiceProvider CreateRequestServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        return services.BuildServiceProvider();
    }

    private static JsonDocument ReadResponseBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;

        return JsonDocument.Parse(httpContext.Response.Body);
    }
}
