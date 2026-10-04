using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Modularity;

using System.Net.Http.Json;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class OpenApiBearerAuthenticationTests
{
    [Theory(DisplayName = "OpenAPI describes Bearer requirements in each version and module without duplicates")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AddHttp_ShouldDescribeProtectedOperations(bool useModule, bool registerTwice)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication("TestBearer").AddBearerToken("TestBearer");
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("Readers", policy => policy.RequireAuthenticatedUser()));
        var module = new HttpModule("users", "Users", typeof(OpenApiBearerAuthenticationTests).Assembly);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"HttpModules:{module.PresentationAssembly.GetName().Name}:Name"] = module.Name,
            [$"HttpModules:{module.PresentationAssembly.GetName().Name}:Title"] = module.Title
        });
        builder.Services.AddHttp(builder.Configuration, useModule ? [module.PresentationAssembly] : []);
        if (registerTwice)
        {
            builder.Services.AddHttp(builder.Configuration, useModule ? [module.PresentationAssembly] : []);
        }

        await using var app = builder.Build();
        var group = app.NewVersionedApi().MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(new ApiVersion(1, 0))
            .HasApiVersion(new ApiVersion(2, 0));
        var common = app.MapGet("/common", () => TypedResults.Ok()).RequireAuthorization();
        if (useModule)
        {
            group.WithGroupName(module.Name);
            common.WithGroupName(module.Name).WithMetadata(module);
        }

        var protectedGroup = group.MapGroup("/protected").RequireAuthorization();
        protectedGroup.MapGet("/user", () => TypedResults.Ok());
        protectedGroup.MapGet("/anonymous", () => TypedResults.Ok()).AllowAnonymous();
        group.MapGet("/public", () => TypedResults.Ok());
        group.MapGet("/named-policy", () => TypedResults.Ok()).RequireAuthorization("Readers");
        group.MapGet("/policy", () => TypedResults.Ok())
            .RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        foreach (var version in new[] { "v1", "v2" })
        {
            var documentName = useModule ? $"{module.Name}-{version}" : version;
            using var document = await client.GetFromJsonAsync<JsonDocument>(
                $"/openapi/{documentName}.json", cancellationToken);

            document.ShouldNotBeNull();
            var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
            schemes.EnumerateObject().Count().ShouldBe(1);
            var scheme = schemes.GetProperty("Bearer");
            scheme.GetProperty("type").GetString().ShouldBe("http");
            scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
            scheme.TryGetProperty("bearerFormat", out _).ShouldBeFalse();
            document.RootElement.TryGetProperty("security", out _).ShouldBeFalse();
            AssertBearerRequired(document, $"/api/{version}/protected/user");
            AssertBearerRequired(document, $"/api/{version}/policy");
            AssertBearerRequired(document, $"/api/{version}/named-policy");
            AssertBearerRequired(document, "/common");
            AssertAnonymous(document, $"/api/{version}/protected/anonymous");
            AssertAnonymous(document, $"/api/{version}/public");
        }
    }

    [Fact(DisplayName = "OpenAPI respects fallback authorization and AllowAnonymous")]
    public async Task AddHttp_ShouldRespectFallbackPolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication("TestBearer").AddBearerToken("TestBearer");
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.MapGet("/protected", () => TypedResults.Ok());
        app.MapGet("/anonymous", () => TypedResults.Ok()).AllowAnonymous();
        app.UseOpenApiConfiguration();
        app.MapOpenApi("/public-openapi/{documentName}.json").WithDocumentPerVersion().AllowAnonymous();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/public-openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        AssertBearerRequired(document, "/protected");
        AssertAnonymous(document, "/anonymous");
    }

    [Theory(DisplayName = "Public APIs do not receive a Bearer scheme or require authorization services")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddHttp_ShouldSupportPublicApplication(bool registerAuthentication)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        if (registerAuthentication)
        {
            builder.Services.AddAuthentication("TestBearer").AddBearerToken("TestBearer");
            builder.Services.AddAuthorization();
        }

        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.MapGet("/public", () => TypedResults.Ok());
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        AssertAnonymous(document, "/public");
        AssertNoSecuritySchemes(document);
    }

    [Fact(DisplayName = "Unversioned module documents keep protected and public security schemes separate")]
    public async Task AddHttp_ShouldIsolateModuleSecuritySchemes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication("TestBearer").AddBearerToken("TestBearer");
        builder.Services.AddAuthorization();
        var users = new HttpModule("users", "Users", typeof(OpenApiBearerAuthenticationTests).Assembly);
        var catalog = new HttpModule("catalog", "Catalog", typeof(OpenApiConfiguration).Assembly);
        builder.Services.AddApiVersioningConfiguration();
        builder.Services.AddOpenApiConfiguration(builder.Configuration, [users, catalog]);
        await using var app = builder.Build();
        app.MapGet("/users", () => TypedResults.Ok()).WithMetadata(users).RequireAuthorization();
        app.MapGet("/catalog", () => TypedResults.Ok()).WithMetadata(catalog);
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var usersDocument = await client.GetFromJsonAsync<JsonDocument>("/openapi/users.json", cancellationToken);
        using var catalogDocument = await client.GetFromJsonAsync<JsonDocument>("/openapi/catalog.json", cancellationToken);

        usersDocument.ShouldNotBeNull();
        catalogDocument.ShouldNotBeNull();
        AssertBearerRequired(usersDocument, "/users");
        usersDocument.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _).ShouldBeTrue();
        AssertAnonymous(catalogDocument, "/catalog");
        AssertNoSecuritySchemes(catalogDocument);
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        return builder;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static void AssertBearerRequired(JsonDocument document, string path)
    {
        var security = document.RootElement.GetProperty("paths").GetProperty(path)
            .GetProperty("get").GetProperty("security");
        security.GetArrayLength().ShouldBe(1);
        security[0].GetProperty("Bearer").GetArrayLength().ShouldBe(0);
    }

    private static void AssertAnonymous(JsonDocument document, string path)
    {
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");
        operation.TryGetProperty("security", out _).ShouldBeFalse();
    }

    private static void AssertNoSecuritySchemes(JsonDocument document)
    {
        var hasSecuritySchemes = document.RootElement.TryGetProperty("components", out var components)
            && components.TryGetProperty("securitySchemes", out _);
        hasSecuritySchemes.ShouldBeFalse();
    }
}
