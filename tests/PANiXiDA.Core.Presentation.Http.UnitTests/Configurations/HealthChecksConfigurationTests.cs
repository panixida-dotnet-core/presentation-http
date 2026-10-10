using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class HealthChecksConfigurationTests
{
    [Theory(DisplayName = "UseHttp maps the configured health path and preserves registered checks")]
    [InlineData("/status/health", true, HttpStatusCode.OK)]
    [InlineData("/status/health", false, HttpStatusCode.ServiceUnavailable)]
    [InlineData("/", true, HttpStatusCode.OK)]
    public async Task UseHttp_ShouldUseConfiguredHealthCheckPath(string path, bool healthy, HttpStatusCode expectedStatus)
    {
        var builder = CreateBuilder(path);
        builder.Services.AddHttp(builder.Configuration);
        builder.Services.AddHealthChecks().AddCheck("probe", () => healthy
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy());
        await using var app = builder.Build();
        app.UseHttp();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        using var defaultResponse = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(healthy ? "Healthy" : "Unhealthy");
        defaultResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "An empty or relative health path prevents startup")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("health")]
    public async Task AddHttp_ShouldRejectInvalidHealthCheckPath(string path)
    {
        var builder = CreateBuilder(path);
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("HealthCheckEndpointOptions.Path");
    }

    private static WebApplicationBuilder CreateBuilder(string path)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthCheckEndpointOptions:Path"] = path
        });
        return builder;
    }
}
