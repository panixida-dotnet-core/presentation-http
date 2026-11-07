using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.Modularity;

using Scalar.AspNetCore;

using System.Net;
using System.Text.Json;

using ScalarOptions = PANiXiDA.Core.Presentation.Http.Options.Scalar.ScalarOptions;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class ScalarOperationOrderingTests
{
    [Theory(DisplayName = "Scalar orders operations using creation metadata in default and module documents")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseOpenApiConfiguration_ShouldExposeCreationMetadata(bool useModule)
    {
        await using var app = await CreateApplicationAsync(useModule: useModule);
        using var client = CreateClient(app);

        var scalar = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync(
            ScalarOperationOrdering.ConfigurationPath, TestContext.Current.CancellationToken);
        var configuration = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = await client.GetStringAsync(
            useModule ? "/openapi/items-v1.json" : "/openapi/v1.json", TestContext.Current.CancellationToken);

        scalar.ShouldContain(ScalarOperationOrdering.ConfigurationPath.TrimStart('/'));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        configuration.ShouldContain(JsonSerializer.Serialize("/api/v1/items"));
        configuration.ShouldContain(JsonSerializer.Serialize("/api/v1/items/personal"));
        configuration.ShouldNotContain(JsonSerializer.Serialize("/api/v1/items/"));
        configuration.ShouldNotContain(JsonSerializer.Serialize("/api/v1/items/{id}/complete"));
        configuration.ShouldNotContain(JsonSerializer.Serialize("/api/v1/items/token"));
        using var parsed = JsonDocument.Parse(document);
        parsed.RootElement.GetProperty("paths")
            .TryGetProperty(ScalarOperationOrdering.ConfigurationPath, out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "Scalar operation ordering supports applications hosted under a path base")]
    public async Task UseOpenApiConfiguration_ShouldSupportPathBase()
    {
        await using var app = await CreateApplicationAsync(pathBase: "/gateway");
        using var client = CreateClient(app);

        var scalar = await client.GetStringAsync("/gateway/scalar", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync(
            "/gateway" + ScalarOperationOrdering.ConfigurationPath, TestContext.Current.CancellationToken);

        scalar.ShouldContain("'scalar/operation-order.js'");
        scalar.ShouldNotContain("'/scalar/operation-order.js'");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
    }

    [Fact(DisplayName = "Scalar operation ordering can be disabled without disabling documentation")]
    public async Task UseOpenApiConfiguration_ShouldAllowDisablingOperationOrdering()
    {
        await using var app = await CreateApplicationAsync(sortOperations: false);
        using var client = CreateClient(app);

        var scalar = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var document = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var routePatterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);

        scalar.ShouldNotContain(ScalarOperationOrdering.ConfigurationPath.TrimStart('/'));
        routePatterns.ShouldNotContain(ScalarOperationOrdering.ConfigurationPath);
        document.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "Scalar preserves explicit host operation sorter and JavaScript configuration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseOpenApiConfiguration_ShouldPreserveHostConfiguration(bool useJavaScript)
    {
        await using var app = await CreateApplicationAsync(configureScalar: options =>
        {
            if (useJavaScript)
            {
                options.WithJavaScriptConfiguration("/custom.js");
            }
            else
            {
                options.OperationSorter = OperationSorter.Method;
            }
        });
        using var client = CreateClient(app);

        var scalar = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);

        scalar.ShouldNotContain(ScalarOperationOrdering.ConfigurationPath.TrimStart('/'));
        scalar.ShouldContain(useJavaScript ? "/custom.js" : "\"operationsSorter\":\"method\"");
    }

    [Theory(DisplayName = "Scalar operation ordering module is not exposed outside Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task UseOpenApiConfiguration_ShouldHideOrderingOutsideDevelopment(string environmentName)
    {
        await using var app = await CreateApplicationAsync(environmentName: environmentName);
        using var client = CreateClient(app);

        using var response = await client.GetAsync(
            ScalarOperationOrdering.ConfigurationPath, TestContext.Current.CancellationToken);
        using var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        using var business = await client.GetAsync("/api/v1/items", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        scalar.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        business.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        bool useModule = false,
        bool? sortOperations = null,
        string environmentName = "Development",
        Action<Scalar.AspNetCore.ScalarOptions>? configureScalar = null,
        string? pathBase = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        if (sortOperations is { } enabled)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ScalarOptions.SectionName}:{nameof(ScalarOptions.SortOperations)}"] = enabled.ToString()
            });
        }
        if (configureScalar is not null)
        {
            builder.Services.Configure(configureScalar);
        }
        var module = new HttpModule("items", "Items", typeof(ScalarOperationOrderingTests).Assembly);
        builder.Services.AddApiVersioningConfiguration();
        builder.Services.AddJsonConfiguration();
        builder.Services.AddOpenApiConfiguration(builder.Configuration, useModule ? [module] : []);
        var app = builder.Build();
        if (pathBase is not null)
        {
            app.UsePathBase(pathBase);
            app.UseRouting();
        }
        var version = new ApiVersion(1, 0);
        var versions = app.NewApiVersionSet().HasApiVersion(version).Build();
        var group = app.MapGroup("/api/v{version:apiVersion}/items")
            .WithApiVersionSet(versions)
            .MapToApiVersion(version);
        if (useModule)
        {
            group.WithGroupName(module.Name).WithMetadata(module);
        }
        group.MapGet("", static () => TypedResults.Ok());
        group.MapPost("/", static () => TypedResults.Created("/api/v1/items/42"));
        group.MapPost("/personal", static () => TypedResults.Created("/api/v1/items/42"));
        group.MapPost("/{id}/complete", static (string id) => TypedResults.Ok(id));
        group.MapPost("/token", static () => TypedResults.Ok());
        app.UseOpenApiConfiguration();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
    }
}
