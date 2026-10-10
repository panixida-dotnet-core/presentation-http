using Asp.Versioning;

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
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
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Readers", policy => policy.RequireAuthenticatedUser());
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
            AssertNoSecurityRequirement(document, $"/api/{version}/protected/anonymous");
            AssertNoSecurityRequirement(document, $"/api/{version}/public");
        }
    }

    [Fact(DisplayName = "OpenAPI respects fallback authorization and AllowAnonymous")]
    public async Task AddHttp_ShouldRespectFallbackPolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
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
        AssertNoSecurityRequirement(document, "/anonymous");
    }

    [Theory(DisplayName = "Public APIs do not receive a Bearer scheme without protected endpoints")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddHttp_ShouldSupportPublicApplication(bool registerAuthentication)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        if (registerAuthentication)
        {
            builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
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
        AssertNoSecurityRequirement(document, "/public");
        AssertNoSecuritySchemes(document);
    }

    [Fact(DisplayName = "Unversioned module documents keep protected and public security schemes separate")]
    public async Task AddHttp_ShouldIsolateModuleSecuritySchemes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
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
        AssertNoSecurityRequirement(catalogDocument, "/catalog");
        AssertNoSecuritySchemes(catalogDocument);
    }

    [Theory(DisplayName = "OpenAPI respects authentication schemes selected by endpoint and named policies")]
    [InlineData("Bearer")]
    [InlineData(BearerTokenDefaults.AuthenticationScheme)]
    [InlineData(CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task AddHttp_ShouldRespectPolicyAuthenticationSchemes(string defaultScheme)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication(defaultScheme).AddBearerToken().AddBearerToken("Bearer").AddCookie();
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("CookieOnly", policy => policy
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .AddPolicy("BearerOnly", policy => policy
                .AddAuthenticationSchemes(BearerTokenDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());
        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.MapGet("/cookie-attribute", () => TypedResults.Ok()).RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme
        });
        app.MapGet("/cookie-policy", () => TypedResults.Ok()).RequireAuthorization(
            new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().Build());
        app.MapGet("/cookie-named", () => TypedResults.Ok()).RequireAuthorization("CookieOnly");
        app.MapGet("/bearer", () => TypedResults.Ok()).RequireAuthorization("BearerOnly");
        app.MapGet("/mixed", () => TypedResults.Ok()).RequireAuthorization("CookieOnly", "BearerOnly");
        app.MapGet("/default", () => TypedResults.Ok()).RequireAuthorization();
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        AssertNoSecurityRequirement(document, "/cookie-attribute");
        AssertNoSecurityRequirement(document, "/cookie-policy");
        AssertNoSecurityRequirement(document, "/cookie-named");
        AssertBearerRequired(document, "/bearer");
        AssertBearerRequired(document, "/mixed");
        if (defaultScheme != CookieAuthenticationDefaults.AuthenticationScheme)
        {
            AssertBearerRequired(document, "/default");
        }
        else
        {
            AssertNoSecurityRequirement(document, "/default");
        }
    }

    [Theory(DisplayName = "OpenAPI respects schemes from default and fallback authorization policies")]
    [InlineData(BearerTokenDefaults.AuthenticationScheme, false)]
    [InlineData(BearerTokenDefaults.AuthenticationScheme, true)]
    [InlineData(CookieAuthenticationDefaults.AuthenticationScheme, false)]
    [InlineData(CookieAuthenticationDefaults.AuthenticationScheme, true)]
    public async Task AddHttp_ShouldRespectDefaultAndFallbackPolicySchemes(string scheme, bool useFallback)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        var defaultScheme = scheme == BearerTokenDefaults.AuthenticationScheme
            ? CookieAuthenticationDefaults.AuthenticationScheme
            : BearerTokenDefaults.AuthenticationScheme;
        builder.Services.AddAuthentication(defaultScheme).AddBearerToken().AddCookie();
        var policy = new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build();
        var authorization = builder.Services.AddAuthorizationBuilder();
        if (useFallback)
        {
            authorization.SetFallbackPolicy(policy);
        }
        else
        {
            authorization.SetDefaultPolicy(policy);
        }

        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        var endpoint = app.MapGet("/protected", () => TypedResults.Ok());
        if (!useFallback)
        {
            endpoint.RequireAuthorization();
        }

        app.MapGet("/anonymous", () => TypedResults.Ok()).RequireAuthorization().AllowAnonymous();
        app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        AssertNoSecurityRequirement(document, "/anonymous");
        if (scheme == BearerTokenDefaults.AuthenticationScheme)
        {
            AssertBearerRequired(document, "/protected");
        }
        else
        {
            AssertNoSecurityRequirement(document, "/protected");
            AssertNoSecuritySchemes(document);
        }
    }

    [Theory(DisplayName = "Custom Bearer scheme names must be explicitly configured for OpenAPI")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddHttp_ShouldRespectConfiguredBearerSchemeNames(bool configureScheme)
    {
        const string scheme = "CustomAccessToken";
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        builder.Services.AddAuthentication(scheme).AddBearerToken(scheme).AddCookie();
        builder.Services.AddAuthorization();
        if (configureScheme)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ScalarConfiguration:BearerAuthenticationSchemes:0"] = scheme
            });
        }

        builder.Services.AddHttp(builder.Configuration);
        await using var app = builder.Build();
        app.MapGet("/default", () => TypedResults.Ok()).RequireAuthorization();
        app.MapGet("/explicit", () => TypedResults.Ok()).RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = scheme
        });
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        if (configureScheme)
        {
            AssertBearerRequired(document, "/default");
            AssertBearerRequired(document, "/explicit");
        }
        else
        {
            AssertNoSecurityRequirement(document, "/default");
            AssertNoSecurityRequirement(document, "/explicit");
            AssertNoSecuritySchemes(document);
        }
    }

    [Theory(DisplayName = "OpenAPI does not infer Bearer when no default authentication scheme is selected")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OpenApi_ShouldNotInferBearerWithoutDefaultScheme(bool registerAuthentication, bool useHttp)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        if (registerAuthentication)
        {
            builder.Services.AddAuthentication().AddBearerToken().AddCookie();
        }
        builder.Services.AddAuthorization();
        if (useHttp)
        {
            builder.Services.AddHttp(builder.Configuration);
        }
        else
        {
            builder.Services.AddApiVersioningConfiguration();
            builder.Services.AddOpenApiConfiguration(builder.Configuration, []);
        }
        await using var app = builder.Build();
        app.MapGet("/protected", () => TypedResults.Ok()).RequireAuthorization();
        app.UseOpenApiConfiguration();
        await app.StartAsync(cancellationToken);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", cancellationToken);

        document.ShouldNotBeNull();
        AssertNoSecurityRequirement(document, "/protected");
        AssertNoSecuritySchemes(document);
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

    private static void AssertNoSecurityRequirement(JsonDocument document, string path)
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
