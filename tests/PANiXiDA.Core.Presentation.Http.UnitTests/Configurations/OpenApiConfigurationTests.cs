using Asp.Versioning;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Modularity;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

using System.Net;
using System.Reflection;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class OpenApiConfigurationTests
{
    [Fact(DisplayName = "OpenAPI configuration registers services and returns the same collection")]
    public void AddOpenApiConfiguration_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddOpenApiConfiguration(configuration, []);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "OpenAPI configuration applies Scalar transformers")]
    public void AddOpenApiConfiguration_ShouldApplyScalarTransformers()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttp(builder.Configuration);
        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get("v1");

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

        services.AddOpenApiConfiguration(configuration, []);

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

        builder.Services.AddOpenApiConfiguration(builder.Configuration, []);

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

    [Theory(DisplayName = "OpenAPI configuration emits strict numeric schemas")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseOpenApiConfiguration_ShouldEmitStrictNumericSchemas(bool explicitlyNeutral)
    {
        await using var app = await CreateStartedStrictSchemaApplicationAsync(
            explicitlyNeutral,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        var countSchema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nameof(NumericResponse))
            .GetProperty("properties")
            .GetProperty("count");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        countSchema
            .GetProperty("type")
            .GetString()
            .ShouldBe("integer");
        countSchema
            .GetProperty("format")
            .GetString()
            .ShouldBe("int32");
        countSchema.TryGetProperty("pattern", out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "OpenAPI configuration isolates documents by HTTP module and version")]
    public async Task UseOpenApiConfiguration_ShouldExposeSeparateModuleDocuments()
    {
        var documentedModuleAssembly = typeof(OrderedEndpointGroup).Assembly;
        var secondModuleAssembly = typeof(OpenApiConfiguration).Assembly;
        var configurationValues = CreateModuleConfigurationValues(
            (documentedModuleAssembly, "tests", "Test endpoints"),
            (secondModuleAssembly, "core", "Core endpoints"));

        await using var app = await CreateStartedModuleApplicationAsync(
            configurationValues,
            [documentedModuleAssembly, secondModuleAssembly],
            TestContext.Current.CancellationToken,
            static app =>
            {
                var versions = app.NewApiVersionSet()
                    .HasApiVersion(new ApiVersion(1, 0))
                    .Build();
                app.MapGet("/api/v{version:apiVersion}/core", static () => "core")
                    .WithGroupName("core")
                    .WithApiVersionSet(versions)
                    .MapToApiVersion(new ApiVersion(1, 0));
                app.MapGet("/connect", static () => "connect")
                    .WithGroupName("tests")
                    .WithMetadata(new HttpModule(
                        "tests",
                        "Test endpoints",
                        typeof(OrderedEndpointGroup).Assembly));
                app.MapGet("/core-connect", static () => "core-connect")
                    .WithGroupName("core")
                    .WithMetadata(new HttpModule(
                        "core",
                        "Core endpoints",
                        typeof(OpenApiConfiguration).Assembly));
                app.MapGet("/unrelated", static () => "unrelated");
            });
        using var client = CreateClient(app);

        using var documentedResponse = await client.GetAsync(
            "/openapi/tests-v1.json",
            TestContext.Current.CancellationToken);
        using var versionTwoResponse = await client.GetAsync(
            "/openapi/tests-v2.json",
            TestContext.Current.CancellationToken);
        using var secondModuleResponse = await client.GetAsync(
            "/openapi/core-v1.json",
            TestContext.Current.CancellationToken);
        using var missingVersionResponse = await client.GetAsync(
            "/openapi/core-v2.json",
            TestContext.Current.CancellationToken);
        using var scalarResponse = await client.GetAsync(
            "/scalar",
            TestContext.Current.CancellationToken);
        var documentedContent = await documentedResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var versionTwoContent = await versionTwoResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var secondModuleContent = await secondModuleResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var scalarContent = await scalarResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var documentedDocument = JsonDocument.Parse(documentedContent);
        using var versionTwoDocument = JsonDocument.Parse(versionTwoContent);
        using var secondModuleDocument = JsonDocument.Parse(secondModuleContent);

        documentedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        versionTwoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondModuleResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        missingVersionResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        scalarResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        documentedDocument.RootElement
            .GetProperty("paths")
            .TryGetProperty("/api/v1/ordered/first", out _)
            .ShouldBeTrue();
        documentedDocument.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .ShouldAllBe(path => path.Name == "/connect" ||
                path.Name.StartsWith("/api/v1/ordered/", StringComparison.Ordinal));
        documentedDocument.RootElement
            .GetProperty("paths")
            .TryGetProperty("/connect", out _)
            .ShouldBeTrue();
        versionTwoDocument.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .Order()
            .ShouldBe(["/api/v2/ordered/first", "/connect"]);
        secondModuleDocument.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .Order()
            .ShouldBe(["/api/v1/core", "/core-connect"]);
        documentedDocument.RootElement
            .GetProperty("info")
            .GetProperty("title")
            .GetString()
            .ShouldBe("Test endpoints v1");
        scalarContent.ShouldContain("Test endpoints v1");
        scalarContent.ShouldContain("Test endpoints v2");
        scalarContent.ShouldContain("Core endpoints v1");
        scalarContent.ShouldContain("openapi/tests-v1.json");
        scalarContent.ShouldContain("openapi/tests-v2.json");
        scalarContent.ShouldContain("openapi/core-v1.json");
        scalarContent.ShouldNotContain("openapi/core-v2.json");
        scalarContent.ShouldNotContain("openapi/tests.json");
        scalarContent.ShouldNotContain("openapi/core.json");
        (await client.GetStringAsync("/connect", TestContext.Current.CancellationToken))
            .ShouldBe("connect");
    }

    [Theory(DisplayName = "OpenAPI configuration exposes a common document for an unversioned module")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseOpenApiConfiguration_ShouldExposeCommonModuleDocument(
        bool explicitlyNeutral,
        bool includeExternalVersions)
    {
        var moduleAssembly = typeof(OrderedEndpointGroup).Assembly;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(CreateModuleConfigurationValues(
            (moduleAssembly, "identity", "Identity endpoints")));
        builder.Services.AddHttp(builder.Configuration, moduleAssembly);
        await using var app = builder.Build();
        app.UseOpenApiConfiguration();
        var group = app.MapGroup("/connect");

        if (explicitlyNeutral)
        {
            group.WithApiVersionSet(app.NewApiVersionSet().Build())
                .IsApiVersionNeutral();
        }

        EndpointMapper.MapGroupEndpoints<OrderedEndpointGroup>(group, app.Services);

        if (includeExternalVersions)
        {
            var versions = app.NewApiVersionSet()
                .HasApiVersion(new ApiVersion(1, 0))
                .HasApiVersion(new ApiVersion(2, 0))
                .Build();
            app.MapGet("/api/v{version:apiVersion}/external", static () => "external")
                .WithApiVersionSet(versions)
                .MapToApiVersion(new ApiVersion(1, 0))
                .MapToApiVersion(new ApiVersion(2, 0));
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        var content = await client.GetStringAsync(
            "/openapi/identity.json",
            TestContext.Current.CancellationToken);
        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);

        document.RootElement.GetProperty("paths")
            .TryGetProperty("/connect/first", out _)
            .ShouldBeTrue();
        document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .ShouldAllBe(path => path.Name.StartsWith("/connect/", StringComparison.Ordinal));
        document.RootElement.GetProperty("info")
            .GetProperty("title")
            .GetString()
            .ShouldBe("Identity endpoints");
        scalarContent.ShouldContain("openapi/identity.json");
        scalarContent.ShouldNotContain("openapi/identity-v1.json");
        if (!includeExternalVersions)
        {
            scalarContent.ShouldNotContain("openapi/v1.json");
        }
        using var response = await client.GetAsync("/connect/first", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "OpenAPI configuration omits modules without endpoints")]
    public async Task UseOpenApiConfiguration_ShouldOmitEmptyModules()
    {
        var moduleAssembly = typeof(OrderedEndpointGroup).Assembly;
        var emptyAssembly = typeof(OpenApiConfiguration).Assembly;
        await using var app = await CreateStartedModuleApplicationAsync(
            CreateModuleConfigurationValues(
                (moduleAssembly, "tests", "Test endpoints"),
                (emptyAssembly, "empty", "Empty module")),
            [moduleAssembly, emptyAssembly],
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/openapi/empty-v1.json", TestContext.Current.CancellationToken);

        scalarContent.ShouldContain("openapi/tests-v1.json");
        scalarContent.ShouldContain("openapi/tests-v2.json");
        scalarContent.ShouldNotContain("Empty module");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "OpenAPI configuration omits versions whose endpoints are excluded from documentation")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseOpenApiConfiguration_ShouldOmitExcludedVersions(bool useModule)
    {
        var assembly = typeof(OrderedEndpointGroup).Assembly;
        await using var app = await CreateStartedModuleApplicationAsync(
            useModule ? CreateModuleConfigurationValues((assembly, "tests", "Test endpoints")) : [],
            useModule ? [assembly] : [],
            TestContext.Current.CancellationToken,
            app =>
            {
                var version = new ApiVersion(3, 0);
                var versions = app.NewApiVersionSet()
                    .HasApiVersion(version)
                    .Build();
                var endpoint = app.MapGet("/api/v{version:apiVersion}/hidden", static () => "hidden")
                    .WithApiVersionSet(versions)
                    .MapToApiVersion(version)
                    .ExcludeFromDescription();

                if (useModule)
                {
                    endpoint.WithGroupName("tests");
                }
            });
        using var client = CreateClient(app);
        var documentPrefix = useModule ? "tests-" : string.Empty;

        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync(
            $"/openapi/{documentPrefix}v3.json",
            TestContext.Current.CancellationToken);

        scalarContent.ShouldContain($"openapi/{documentPrefix}v1.json");
        scalarContent.ShouldNotContain($"openapi/{documentPrefix}v3.json");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetStringAsync("/api/v3/hidden", TestContext.Current.CancellationToken))
            .ShouldBe("hidden");
    }

    [Theory(DisplayName = "OpenAPI configuration exposes version documents without HTTP modules")]
    [InlineData("v1", "/api/v1/ordered/first", "/api/v2/ordered/first")]
    [InlineData("v2", "/api/v2/ordered/first", "/api/v1/ordered/first")]
    public async Task UseOpenApiConfiguration_ShouldExposeVersionsWithoutModules(
        string documentName,
        string includedPath,
        string excludedPath)
    {
        await using var app = await CreateStartedModuleApplicationAsync(
            [],
            [],
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        var content = await client.GetStringAsync(
            $"/openapi/{documentName}.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty(includedPath, out _).ShouldBeTrue();
        paths.TryGetProperty(excludedPath, out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "OpenAPI configuration does not map the specification or Scalar endpoints outside Development")]
    public void UseOpenApiConfiguration_ShouldNotMapOpenApiOrScalarEndpointsOutsideDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddOpenApiConfiguration(builder.Configuration, []);

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
        builder.Services.AddOpenApiConfiguration(builder.Configuration, []);

        var app = builder.Build();
        app.UseOpenApiConfiguration();

        await app.StartAsync(cancellationToken);

        return app;
    }

    private static async Task<WebApplication> CreateStartedModuleApplicationAsync(
        Dictionary<string, string?> configurationValues,
        Assembly[] moduleAssemblies,
        CancellationToken cancellationToken,
        Action<WebApplication>? configureEndpoints = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddHttp(builder.Configuration, moduleAssemblies);

        var app = builder.Build();
        app.UseHttp(typeof(OrderedEndpointGroup).Assembly);
        configureEndpoints?.Invoke(app);

        await app.StartAsync(cancellationToken);

        return app;
    }

    private static async Task<WebApplication> CreateStartedStrictSchemaApplicationAsync(
        bool explicitlyNeutral,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);

        var app = builder.Build();
        var endpoint = app.MapGet("/numeric", static () => new NumericResponse(Count: 1));

        if (explicitlyNeutral)
        {
            endpoint.WithApiVersionSet(app.NewApiVersionSet().Build())
                .IsApiVersionNeutral();
        }

        app.UseHttp();

        await app.StartAsync(cancellationToken);

        return app;
    }

    private static Dictionary<string, string?> CreateModuleConfigurationValues(
        params (Assembly Assembly, string Name, string Title)[] modules)
    {
        var values = new Dictionary<string, string?>();

        foreach (var module in modules)
        {
            var assemblyName = module.Assembly.GetName().Name;
            values[$"HttpModules:{assemblyName}:Name"] = module.Name;
            values[$"HttpModules:{assemblyName}:Title"] = module.Title;
        }

        return values;
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

    private sealed record NumericResponse(int Count);
}
