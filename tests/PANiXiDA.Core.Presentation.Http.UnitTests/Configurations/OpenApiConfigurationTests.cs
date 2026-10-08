using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Authentication.BearerToken;
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

    [Fact(DisplayName = "OpenAPI configuration binds Scalar API reference title and favicon")]
    public void AddOpenApiConfiguration_ShouldBindScalarApiReferenceTitleAndFavicon()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Title)] = "Orders API Reference",
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Favicon)] = "/favicon.svg"
            })
            .Build();

        services.AddOpenApiConfiguration(configuration, []);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ScalarConfiguration>>().Value;

        options.Title.ShouldBe("Orders API Reference");
        options.Favicon.ShouldBe("/favicon.svg");
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

    [Theory(DisplayName = "OpenAPI configuration short-circuits favicon requests after routing")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseOpenApiConfiguration_ShouldServeConfiguredHostFavicon(
        bool mapStaticAssetsExplicitly,
        bool useSlimBuilder)
    {
        await using var app = await CreateStartedApplicationAsync(
            new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Favicon)] = "/favicon.svg"
            },
            TestContext.Current.CancellationToken,
            configureApplication: application =>
            {
                application.Use(async (context, next) =>
                {
                    context.Response.Headers["X-Before-Routing"] = "executed";
                    await next(context);
                });
                if (mapStaticAssetsExplicitly)
                {
                    application.MapStaticAssets();
                }
            },
            useSlimBuilder: useSlimBuilder,
            configureAfterHttp: application => application.Use(async (context, next) =>
            {
                context.Response.Headers["X-Test-Middleware"] = "executed";
                await next(context);
            }));
        using var client = CreateClient(app);

        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var healthResponse = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/favicon.svg", TestContext.Current.CancellationToken);
        var favicon = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        favicon.ShouldNotBeEmpty();
        await using var expectedStream = app.Environment.WebRootFileProvider.GetFileInfo("favicon.svg")
            .CreateReadStream();
        using var expectedContent = new MemoryStream();
        await expectedStream.CopyToAsync(expectedContent, TestContext.Current.CancellationToken);

        scalarContent.ShouldContain("\"favicon\":\"/favicon.svg\"");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/svg+xml");
        response.Headers.GetValues("X-Before-Routing").ShouldBe(["executed"]);
        response.Headers.Contains("X-Test-Middleware").ShouldBeFalse();
        favicon.ShouldBe(expectedContent.ToArray());
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        healthResponse.Headers.GetValues("X-Test-Middleware").ShouldBe(["executed"]);
    }

    [Fact(DisplayName = "Short-circuited assets preserve host HTTPS and HSTS middleware and API authorization")]
    public async Task UseOpenApiConfiguration_ShouldPreserveHostMiddlewareAndApiAuthorization()
    {
        await using var app = await CreateStartedApplicationAsync(
            new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Favicon)] = "/favicon.svg"
            },
            TestContext.Current.CancellationToken,
            configureApplication: application =>
            {
                application.UseForwardedHeaders();
                application.UseHsts();
                application.MapGet("/protected", static () => TypedResults.Ok()).RequireAuthorization();
            },
            configureBuilder: builder =>
            {
                builder.Services.AddHttpsRedirection(options => options.HttpsPort = 8443);
                builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
                builder.Services.AddAuthorization();
            });
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/favicon.svg");
        httpRequest.Headers.Host = "assets.example";
        using var httpsRequest = new HttpRequestMessage(HttpMethod.Get, "/favicon.svg");
        httpsRequest.Headers.Host = "assets.example";
        httpsRequest.Headers.Add("X-Forwarded-Proto", "https");
        using var protectedRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        protectedRequest.Headers.Add("X-Forwarded-Proto", "https");

        using var httpResponse = await client.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        using var httpsResponse = await client.SendAsync(httpsRequest, TestContext.Current.CancellationToken);
        using var protectedResponse = await client.SendAsync(protectedRequest, TestContext.Current.CancellationToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        httpResponse.Headers.Location.ShouldBe(new Uri("https://assets.example:8443/favicon.svg"));
        httpsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        httpsResponse.Headers.Contains("Strict-Transport-Security").ShouldBeTrue();
        (await httpsResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldNotBeEmpty();
        protectedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory(DisplayName = "OpenAPI configuration keeps the default favicon and does not map assets for a blank favicon")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task UseOpenApiConfiguration_ShouldIgnoreBlankFavicon(string? favicon)
    {
        await using var app = await CreateStartedApplicationAsync(
            new Dictionary<string, string?>
            {
                [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Favicon)] = favicon
            },
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/favicon.svg", TestContext.Current.CancellationToken);

        scalarContent.ShouldContain("favicon.svg");
        scalarContent.ShouldNotContain("\"favicon\":\"/favicon.svg\"");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
                app.MapGroup(EndpointConstants.EndpointPrefix)
                    .MapGet("/core", static () => "core")
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
            app.MapGroup(EndpointConstants.EndpointPrefix)
                .MapGet("/external", static () => "external")
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

    [Theory(DisplayName = "OpenAPI configuration rejects duplicate final document names ignoring case")]
    [InlineData(true, "orders-v1", "orders-v1")]
    [InlineData(true, "ORDERS-V1", "orders-v1")]
    [InlineData(false, "v1", "v1")]
    [InlineData(false, "V1", "v1")]
    public async Task AddOpenApiConfiguration_ShouldRejectDuplicateDocumentNames(
        bool useVersionedModule,
        string commonDocumentName,
        string duplicateDocumentName)
    {
        var commonModuleAssembly = typeof(OrderedEndpointGroup).Assembly;
        var versionedModuleAssembly = typeof(OpenApiConfiguration).Assembly;
        var configurationValues = CreateModuleConfigurationValues(
            (commonModuleAssembly, commonDocumentName, "Common endpoints"));

        if (useVersionedModule)
        {
            foreach (var value in CreateModuleConfigurationValues(
                (versionedModuleAssembly, "orders", "Order endpoints")))
            {
                configurationValues.Add(value.Key, value.Value);
            }
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddHttp(
            builder.Configuration,
            useVersionedModule ? [commonModuleAssembly, versionedModuleAssembly] : [commonModuleAssembly]);
        await using var app = builder.Build();
        app.MapGet("/connect", static () => "connect")
            .WithGroupName(commonDocumentName)
            .WithMetadata(new HttpModule(
                commonDocumentName,
                "Common endpoints",
                commonModuleAssembly));
        var versions = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .Build();
        var endpoint = app.MapGroup(EndpointConstants.EndpointPrefix)
            .MapGet("/orders", static () => "orders")
            .WithApiVersionSet(versions)
            .MapToApiVersion(new ApiVersion(1, 0));

        if (useVersionedModule)
        {
            endpoint.WithGroupName("orders");
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        var exception = Should.Throw<InvalidOperationException>(() => provider.ApiVersionDescriptions);

        exception.Message.ShouldBe(
            $"The OpenAPI document name '{duplicateDocumentName}' is already registered. Configure unique HTTP module names.");
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseOpenApiConfiguration_ShouldOmitExcludedVersions(
        bool useModule,
        bool includeUnversioned)
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
                var endpoint = app.MapGroup(EndpointConstants.EndpointPrefix)
                    .MapGet("/hidden", static () => "hidden")
                    .WithApiVersionSet(versions)
                    .MapToApiVersion(version)
                    .ExcludeFromDescription();

                if (useModule)
                {
                    endpoint.WithGroupName("tests");
                }

                if (includeUnversioned)
                {
                    var commonEndpoint = app.MapGet("/connect", static () => "connect");

                    if (useModule)
                    {
                        commonEndpoint.WithGroupName("tests");
                    }
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
        var content = await client.GetStringAsync(
            $"/openapi/{documentPrefix}v1.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("paths")
            .TryGetProperty("/connect", out _)
            .ShouldBe(includeUnversioned);
        (await client.GetStringAsync("/api/v3/hidden", TestContext.Current.CancellationToken))
            .ShouldBe("hidden");
    }

    [Theory(DisplayName = "OpenAPI configuration uses only the default document when all versioned endpoints are hidden")]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task UseOpenApiConfiguration_ShouldUseDefaultDocumentForUnversionedEndpoints(
        bool explicitlyNeutral,
        int defaultVersion)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);
        builder.Services.Configure<ApiVersioningOptions>(options =>
            options.DefaultApiVersion = new ApiVersion(defaultVersion, 0));
        await using var app = builder.Build();
        app.UseHttp();
        var endpoint = app.MapGet("/connect", static () => "connect");

        if (explicitlyNeutral)
        {
            endpoint.WithApiVersionSet(app.NewApiVersionSet().Build())
                .IsApiVersionNeutral();
        }

        var versions = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(3, 0))
            .Build();
        app.MapGroup(EndpointConstants.EndpointPrefix)
            .MapGet("/hidden", static () => "hidden")
            .WithApiVersionSet(versions)
            .MapToApiVersion(new ApiVersion(3, 0))
            .ExcludeFromDescription();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        var scalarContent = await client.GetStringAsync("/scalar", TestContext.Current.CancellationToken);
        using var hiddenResponse = await client.GetAsync(
            "/openapi/v3.json",
            TestContext.Current.CancellationToken);

        scalarContent.ShouldContain($"openapi/v{defaultVersion}.json");
        scalarContent.ShouldNotContain("openapi/v3.json");
        hiddenResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await client.GetStringAsync(
            $"/openapi/v{defaultVersion}.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .ShouldBe(["/connect"]);
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

    [Theory(DisplayName = "OpenAPI configuration does not map OpenAPI, Scalar, or favicon assets outside Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void UseOpenApiConfiguration_ShouldNotMapOpenApiOrScalarEndpointsOutsideDevelopment(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(ScalarConfiguration) + ":" + nameof(ScalarConfiguration.Favicon)] = "/favicon.svg"
        });
        builder.Services.AddOpenApiConfiguration(builder.Configuration, []);

        using var app = builder.Build();

        var result = app.UseOpenApiConfiguration();

        result.ShouldBeSameAs(app);
        var routePatterns = GetRoutePatterns(app);

        routePatterns.ShouldNotContain("/openapi/{documentName}.json");
        routePatterns.ShouldNotContain("/scalar/{documentName?}");
        routePatterns.ShouldNotContain("favicon.svg");
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
        CancellationToken cancellationToken,
        Action<WebApplication>? configureApplication = null,
        bool useSlimBuilder = false,
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<WebApplication>? configureAfterHttp = null)
    {
        var options = new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(OpenApiConfigurationTests).Assembly.GetName().Name
        };
        var builder = useSlimBuilder
            ? WebApplication.CreateSlimBuilder(options)
            : WebApplication.CreateBuilder(options);

        if (useSlimBuilder)
        {
            builder.WebHost.UseStaticWebAssets();
        }

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddHttp(builder.Configuration);
        configureBuilder?.Invoke(builder);

        var app = builder.Build();
        configureApplication?.Invoke(app);
        app.UseHttp();
        configureAfterHttp?.Invoke(app);

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
