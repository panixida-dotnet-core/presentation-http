using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddHttp registers HTTP infrastructure and returns the same service collection")]
    public void AddHttp_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddHttp(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "AddHttp applies ForwardedHeaders configuration")]
    public void AddHttp_ShouldApplyForwardedHeadersConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(ForwardedHeadersOptions.ForwardLimit)] = "4"
            })
            .Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.ShouldBe(4);
    }

    [Fact(DisplayName = "AddHttp applies ForwardedHeaders section from root configuration")]
    public void AddHttp_ShouldApplyForwardedHeadersSectionFromRootConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:" + nameof(ForwardedHeadersOptions.ForwardLimit)] = "6"
            })
            .Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.ShouldBe(6);
    }

    [Fact(DisplayName = "AddHttp registers health check services")]
    public void AddHttp_ShouldRegisterHealthCheckServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        healthCheckService.ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddHttp uses default settings when configuration is empty")]
    public void AddHttp_ShouldUseDefaultSettingsWhenConfigurationIsEmpty()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddHttp(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "UseHttp adds middleware and maps groups from the provided assemblies")]
    public void UseHttp_ShouldReturnSameApplicationAndMapEndpointGroups()
    {
        EndpointMappingRecorder.Clear();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddHttp(builder.Configuration);

        using var app = builder.Build();

        var result = app.UseHttp(typeof(ADiscoveredEndpointGroup).Assembly);

        result.ShouldBeSameAs(app);
        EndpointMappingRecorder.Entries.ShouldContain(nameof(ADiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.ShouldContain(nameof(BDiscoveredEndpointGroup));
    }

    [Fact(DisplayName = "UseHttp uses ProblemDetails exception handler in Development")]
    public async Task UseHttp_ShouldUseProblemDetailsExceptionHandlerInDevelopment()
    {
        await using var app = await CreateStartedThrowingApplicationAsync(
            Environments.Development,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        content.ShouldContain("Internal server error");
        content.ShouldContain("Pipeline development failure");
        content.ShouldNotContain(nameof(InvalidOperationException));
    }

    [Fact(DisplayName = "UseHttp uses ProblemDetails exception handler outside Development")]
    public async Task UseHttp_ShouldUseProblemDetailsExceptionHandlerOutsideDevelopment()
    {
        await using var app = await CreateStartedThrowingApplicationAsync(
            Environments.Production,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        content.ShouldContain("Internal server error");
        content.ShouldNotContain("Pipeline development failure");
    }

    [Fact(DisplayName = "UseHttp maps the health check endpoint")]
    public async Task UseHttp_ShouldMapHealthCheckEndpoint()
    {
        await using var app = await CreateStartedApplicationAsync(
            Environments.Production,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldBe("Healthy");
    }

    private static async Task<WebApplication> CreateStartedThrowingApplicationAsync(
        string environmentName,
        CancellationToken cancellationToken)
    {
        return await CreateStartedApplicationAsync(environmentName, cancellationToken, app =>
        {
            app.MapGet("/throw", static () =>
            {
                throw new InvalidOperationException("Pipeline development failure");
            });
        });
    }

    private static async Task<WebApplication> CreateStartedApplicationAsync(
        string environmentName,
        CancellationToken cancellationToken,
        Action<WebApplication>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);

        var app = builder.Build();
        app.UseHttp();
        configure?.Invoke(app);

        await app.StartAsync(cancellationToken);

        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server addresses feature is not available.");
        var address = addresses.Addresses.Single();

        return new HttpClient
        {
            BaseAddress = new Uri(address)
        };
    }
}
