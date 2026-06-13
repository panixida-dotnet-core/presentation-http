using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class OpenApiConfigurationTests
{
    [Fact(DisplayName = "OpenAPI configuration registers services and returns the same collection")]
    public void AddOpenApiConfiguration_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddOpenApiConfiguration();

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "OpenAPI configuration applies Scalar transformers")]
    public void AddOpenApiConfiguration_ShouldApplyScalarTransformers()
    {
        var services = new ServiceCollection();

        services.AddOpenApiConfiguration();

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get("v1");

        options.ShouldNotBeNull();
    }

    [Fact(DisplayName = "OpenAPI configuration maps the specification and Scalar endpoints in Development")]
    public void UseOpenApiConfiguration_ShouldMapOpenApiAndScalarEndpointsInDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddOpenApiConfiguration();

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.ShouldBeSameAs(app);
        var routePatterns = GetRoutePatterns(app);

        routePatterns.ShouldContain("/openapi/{documentName}.json");
        routePatterns.ShouldContain("/scalar/{documentName?}");
    }

    [Fact(DisplayName = "OpenAPI configuration does not map the specification or Scalar endpoints outside Development")]
    public void UseOpenApiConfiguration_ShouldNotMapOpenApiOrScalarEndpointsOutsideDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

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
}
