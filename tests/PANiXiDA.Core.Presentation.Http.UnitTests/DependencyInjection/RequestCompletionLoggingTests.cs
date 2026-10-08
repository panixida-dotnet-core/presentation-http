using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class RequestCompletionLoggingTests
{
    [Theory(DisplayName = "UseHttp logs the final response status after exception handling")]
    [InlineData(true, true, StatusCodes.Status499ClientClosedRequest, LogLevel.Information)]
    [InlineData(false, true, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(true, false, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    [InlineData(false, false, StatusCodes.Status500InternalServerError, LogLevel.Error)]
    public async Task UseHttp_ShouldLogFinalResponseStatus(
        bool requestAborted,
        bool cancellationException,
        int expectedStatus,
        LogLevel expectedLevel)
    {
        var logger = new TestLogger<LoggingMiddleware>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddHttp(builder.Configuration);
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-42")], "test"));
            await next(context);
            completed.SetResult();
        });
        app.UseHttp();
        app.MapGet("/throw", (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(requestAborted);
            if (cancellationException)
            {
                throw new OperationCanceledException(context.RequestAborted);
            }

            throw new InvalidOperationException("Independent application failure");
        }).WithDisplayName("Throw endpoint");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)expectedStatus);
        logger.Entries.ShouldHaveSingleItem().LogLevel.ShouldBe(expectedLevel);
        var scopes = logger.Scopes.Cast<IReadOnlyDictionary<string, object?>>().ToArray();
        scopes.Single(scope => scope.ContainsKey("http.response.status_code"))["http.response.status_code"].ShouldBe(expectedStatus);
        var requestScope = scopes.Single(scope => scope.ContainsKey("http.route"));
        requestScope["http.route"].ShouldBe("/throw");
        requestScope["aspnetcore.endpoint.display_name"].ShouldBe("Throw endpoint");
        requestScope["enduser.id"].ShouldBe("user-42");
    }
}
