using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class OpenApiConfigurationTests
{
    [Fact(DisplayName = "OpenAPI configuration registers services and returns the same collection")]
    public void AddOpenApiConfiguration_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddOpenApiConfiguration(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "OpenAPI configuration applies Scalar transformers")]
    public void AddOpenApiConfiguration_ShouldApplyScalarTransformers()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddOpenApiConfiguration(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get("v1");

        options.ShouldNotBeNull();
    }

    [Fact(DisplayName = "OpenAPI configuration binds Scalar API reference title")]
    public void AddOpenApiConfiguration_ShouldBindScalarApiReferenceTitle()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Title)] = "Orders API Reference"
            })
            .Build();

        services.AddOpenApiConfiguration(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ScalarConfiguration>>().Value;

        options.Title.ShouldBe("Orders API Reference");
    }

    [Fact(DisplayName = "OpenAPI configuration maps the specification and Scalar endpoints in Development")]
    public void UseOpenApiConfiguration_ShouldMapOpenApiAndScalarEndpointsInDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddOpenApiConfiguration(builder.Configuration);

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.ShouldBeSameAs(app);
        var routePatterns = GetRoutePatterns(app);

        routePatterns.ShouldContain("/openapi/{documentName}.json");
        routePatterns.ShouldContain("/scalar/{documentName?}");
    }

    [Fact(DisplayName = "OpenAPI configuration uses configured Scalar document title in Development")]
    public async Task UseOpenApiConfiguration_ShouldUseConfiguredScalarDocumentTitleInDevelopment()
    {
        await using var app = await CreateStartedApplicationAsync(
            new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Title)] = "Orders API Reference"
            },
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldContain("<title>Orders API Reference</title>");
    }

    [Fact(DisplayName = "OpenAPI configuration does not map the specification or Scalar endpoints outside Development")]
    public void UseOpenApiConfiguration_ShouldNotMapOpenApiOrScalarEndpointsOutsideDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddOpenApiConfiguration(builder.Configuration);

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.ShouldBeSameAs(app);
        var routePatterns = GetRoutePatterns(app);

        routePatterns.ShouldNotContain("/openapi/{documentName}.json");
        routePatterns.ShouldNotContain("/scalar/{documentName?}");
    }

    private static List<string?> GetRoutePatterns(WebApplication app)
    {
        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)];
    }

    private static async Task<WebApplication> CreateStartedApplicationAsync(
        Dictionary<string, string?> configurationValues,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddOpenApiConfiguration(builder.Configuration);

        var app = builder.Build();
        app.UseOpenApiConfiguration();

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
