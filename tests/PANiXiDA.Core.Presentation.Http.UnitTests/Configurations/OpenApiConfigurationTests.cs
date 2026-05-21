using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class OpenApiConfigurationTests
{
    [Fact(DisplayName = "OpenAPI configuration registers services and returns the same collection")]
    public void AddOpenApiConfiguration_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddOpenApiConfiguration();

        result.Should().BeSameAs(services);
    }

    [Fact(DisplayName = "OpenAPI configuration maps the specification endpoint in Development")]
    public void UseOpenApiConfiguration_ShouldMapOpenApiEndpointInDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.Services.AddOpenApiConfiguration();

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.Should().BeSameAs(app);
        GetRoutePatterns(app).Should().Contain("/openapi/{documentName}.json");
    }

    [Fact(DisplayName = "OpenAPI configuration does not map the specification endpoint outside Development")]
    public void UseOpenApiConfiguration_ShouldNotMapOpenApiEndpointOutsideDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.Should().BeSameAs(app);
        GetRoutePatterns(app).Should().NotContain("/openapi/{documentName}.json");
    }

    private static List<string?> GetRoutePatterns(WebApplication app)
    {
        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)];
    }
}
