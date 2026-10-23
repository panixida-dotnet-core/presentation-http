using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenIddict.Validation.SystemNetHttp;
using OpenIddict.Validation.AspNetCore;

using PANiXiDA.Core.Application.Authentication.Abstractions;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Authentication;

public sealed class TokenIntrospectionTests
{
    private const string CookieUserId = "01992746-2c04-7f48-b5a0-c42487c72454";

    [Fact(DisplayName = "Explicit policies reject invalid tokens without additional introspection")]
    public async Task ExplicitScheme_ShouldRejectInvalidToken()
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity,
            schemes: [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme], cookieDefault: true);
        using var client = CreateClient(app, "invalid");

        using var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        identity.IntrospectedTokens.ShouldBe(["invalid"]);
    }

    [Fact(DisplayName = "Combined policies preserve the Bearer token when the cookie scheme does not authenticate")]
    public async Task CombinedSchemes_ShouldIgnoreMissingCookie()
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity,
            schemes: [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme], cookieDefault: true);
        using var client = CreateClient(app, "valid");

        var profile = await client.GetFromJsonAsync<Profile>("/me", TestContext.Current.CancellationToken);

        profile.ShouldNotBeNull();
        profile.AccessToken.ShouldBe("valid");
        identity.IntrospectedTokens.ShouldBe(["valid"]);
    }

    [Theory(DisplayName = "Explicit Bearer policies preserve the saved access token regardless of the default scheme")]
    [InlineData(false, "valid")]
    [InlineData(true, "valid")]
    [InlineData(true, "service")]
    public async Task ExplicitScheme_ShouldPreserveAccessToken(bool cookieDefault, string token)
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity,
            schemes: [OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme], cookieDefault: cookieDefault);
        using var client = CreateClient(app, token);

        var profile = await client.GetFromJsonAsync<Profile>("/me", TestContext.Current.CancellationToken);

        profile.ShouldNotBeNull();
        profile.AccessToken.ShouldBe(token);
        identity.IntrospectedTokens.ShouldBe([token]);
    }

    [Theory(DisplayName = "Combined policies keep the token belonging to the first authenticated identity")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CombinedSchemes_ShouldPreserveCurrentIdentityToken(bool cookieLast, bool cookieToken)
    {
        var identity = new IdentityServerHandler();
        string[] schemes = [CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme];
        if (cookieLast)
        {
            Array.Reverse(schemes);
        }
        await using var app = await CreateApplicationAsync(identity,
            schemes: schemes, cookieDefault: true, cookieToken: cookieToken);
        using var client = CreateClient(app);
        using var login = await client.GetAsync("/cookie-login", TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid");

        var profile = await client.GetFromJsonAsync<Profile>("/me", TestContext.Current.CancellationToken);

        profile.ShouldNotBeNull();
        profile.UserId.ShouldBe(cookieLast ? Guid.Parse(CookieUserId) : IdentityServerHandler.UserId);
        var expectedToken = cookieLast ? null : "valid";
        if (cookieLast && cookieToken)
        {
            expectedToken = "cookie-token";
        }
        profile.AccessToken.ShouldBe(expectedToken);
        identity.IntrospectedTokens.ShouldBe(["valid"]);
    }

    [Theory(DisplayName = "Bearer introspection populates the current user and enforces role and permission policies")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidToken_ShouldAuthenticateCurrentUser(bool registerTwice)
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity, registerTwice);
        using var client = CreateClient(app, "valid");

        var profile = await client.GetFromJsonAsync<Profile>("/me", TestContext.Current.CancellationToken);
        var allowed = await client.GetAsync("/review", TestContext.Current.CancellationToken);
        var forbidden = await client.GetAsync("/admin", TestContext.Current.CancellationToken);

        profile.ShouldNotBeNull();
        profile.UserId.ShouldBe(IdentityServerHandler.UserId);
        profile.Subject.ShouldBe(IdentityServerHandler.UserId.ToString());
        profile.Name.ShouldBe("Test User");
        profile.Roles.ShouldBe(["Worker", "Reviewer"], ignoreOrder: true);
        profile.CanRead.ShouldBeTrue();
        profile.AccessToken.ShouldBe("valid");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        identity.IntrospectedTokens.ShouldBe(["valid", "valid", "valid"]);
    }

    [Theory(DisplayName = "Invalid tokens and invalid Identity responses cannot access protected endpoints")]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-issuer", HttpStatusCode.InternalServerError)]
    [InlineData("refresh", HttpStatusCode.Unauthorized)]
    public async Task InvalidToken_ShouldBeRejected(string token, HttpStatusCode status)
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app, token);

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        response.Headers.Location.ShouldBeNull();
        response.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
        identity.IntrospectedTokens.ShouldHaveSingleItem().ShouldBe(token);
    }

    [Theory(DisplayName = "Only Authorization headers supply access tokens")]
    [InlineData("none")]
    [InlineData("cookie")]
    [InlineData("query")]
    [InlineData("form")]
    public async Task MissingBearerHeader_ShouldReturnUnauthorized(string source)
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            source == "query" ? "/me?access_token=valid" : "/me");
        if (source == "cookie")
        {
            request.Headers.Add("Cookie", "access_token=valid");
        }
        if (source == "form")
        {
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = "valid" });
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        identity.IntrospectedTokens.ShouldBeEmpty();
    }

    [Theory(DisplayName = "Public endpoints expose no access token for absent or rejected authentication")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("refresh")]
    public async Task PublicEndpoint_ShouldNotExposeUnauthenticatedToken(string? token)
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app, token);

        var response = await client.GetAsync("/public", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<Profile>(TestContext.Current.CancellationToken);
        profile.ShouldNotBeNull();
        profile.AccessToken.ShouldBeNull();
        identity.IntrospectedTokens.ShouldBe(token is null ? [] : [token]);
    }

    [Fact(DisplayName = "Revocation takes effect on the next request")]
    public async Task RevokedToken_ShouldBeRejectedOnNextRequest()
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app, "valid");

        var before = await client.GetAsync("/me", TestContext.Current.CancellationToken);
        identity.IsRevoked = true;
        var after = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        identity.IntrospectedTokens.ShouldBe(["valid", "valid"]);
    }

    [Fact(DisplayName = "Service tokens preserve string subjects without inventing user GUIDs")]
    public async Task ServiceToken_ShouldPreserveSubject()
    {
        await using var app = await CreateApplicationAsync(new IdentityServerHandler());
        using var client = CreateClient(app, "service");

        var profile = await client.GetFromJsonAsync<Profile>("/me", TestContext.Current.CancellationToken);

        profile.ShouldNotBeNull();
        profile.Subject.ShouldBe("background-worker");
        profile.UserId.ShouldBeNull();
        profile.AccessToken.ShouldBe("service");
    }

    [Fact(DisplayName = "An unavailable Identity cannot grant access")]
    public async Task UnavailableIdentity_ShouldFailClosed()
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app, "unavailable");

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeFalse();
        identity.IntrospectedTokens.ShouldHaveSingleItem();
    }

    [Fact(DisplayName = "OpenAPI recognizes configured introspection without extra scheme configuration")]
    public async Task OpenApi_ShouldDescribeIntrospectionScheme()
    {
        var identity = new IdentityServerHandler();
        await using var app = await CreateApplicationAsync(identity);
        using var client = CreateClient(app);

        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json", TestContext.Current.CancellationToken);

        document.ShouldNotBeNull();
        document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString().ShouldBe("bearer");
        document.RootElement.GetProperty("paths").GetProperty("/me").GetProperty("get")
            .GetProperty("security")[0].GetProperty("Bearer").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("paths").GetProperty("/public").GetProperty("get")
            .TryGetProperty("security", out _).ShouldBeFalse();
        identity.IntrospectedTokens.ShouldBeEmpty();
    }

    [Theory(DisplayName = "JSON endpoints use OpenIddict Bearer authentication without antiforgery tokens")]
    [InlineData("/json", null, HttpStatusCode.Unauthorized)]
    [InlineData("/json", "invalid", HttpStatusCode.Unauthorized)]
    [InlineData("/json", "valid", HttpStatusCode.OK)]
    [InlineData("/public-json", null, HttpStatusCode.OK)]
    public async Task JsonEndpoint_ShouldNotRequireAntiforgeryToken(string path, string? token, HttpStatusCode expectedStatus)
    {
        await using var app = await CreateApplicationAsync(new IdentityServerHandler());
        using var client = CreateClient(app, token);

        using var response = await client.PostAsJsonAsync(path, new Payload("submitted"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        if (expectedStatus == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("submitted");
        }
    }

    [Theory(DisplayName = "Authentication and authorization precede antiforgery and Bearer does not bypass it")]
    [InlineData("/form", null, HttpStatusCode.Unauthorized)]
    [InlineData("/form", "invalid", HttpStatusCode.Unauthorized)]
    [InlineData("/form", "valid", HttpStatusCode.BadRequest)]
    [InlineData("/admin-form", "valid", HttpStatusCode.Forbidden)]
    [InlineData("/form-optout", "valid", HttpStatusCode.OK)]
    [InlineData("/form-optout", null, HttpStatusCode.Unauthorized)]
    public async Task FormEndpoint_ShouldEnforceAuthenticationBeforeAntiforgery(
        string path, string? token, HttpStatusCode expectedStatus)
    {
        await using var app = await CreateApplicationAsync(new IdentityServerHandler());
        using var client = CreateClient(app, token);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = "submitted" });

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact(DisplayName = "Antiforgery validates tokens against the authenticated OpenIddict user")]
    public async Task FormEndpoint_ShouldAcceptTokenForAuthenticatedUser()
    {
        await using var app = await CreateApplicationAsync(new IdentityServerHandler());
        using var client = CreateClient(app, "valid");
        var token = await client.GetStringAsync("/antiforgery-token", TestContext.Current.CancellationToken);
        var options = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        client.DefaultRequestHeaders.Add(options.HeaderName!, token);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = "submitted" });

        using var response = await client.PostAsync("/form", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("submitted");
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        IdentityServerHandler identity, bool registerTwice = false,
        IReadOnlyList<string>? schemes = null, bool cookieDefault = false, bool cookieToken = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddictValidationOptions:Issuer"] = IdentityServerHandler.Issuer,
            ["OpenIddictValidationOptions:Audiences:0"] = IdentityServerHandler.Audience,
            ["OpenIddictValidationOptions:ClientId"] = IdentityServerHandler.Audience,
            ["OpenIddictValidationOptions:ClientSecret"] = IdentityServerHandler.ClientSecret
        });
        builder.Services.AddHttp(builder.Configuration);
        if (registerTwice)
        {
            builder.Services.AddHttp(builder.Configuration);
        }
        if (cookieDefault)
        {
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => options.Cookie.SecurePolicy = CookieSecurePolicy.None);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        }
        var authorization = builder.Services.AddAuthorizationBuilder();
        if (schemes is not null)
        {
            authorization.SetDefaultPolicy(new AuthorizationPolicyBuilder([.. schemes])
                .RequireAuthenticatedUser().Build());
        }
        authorization
            .AddPolicy("review", policy => policy.RequireRole("Reviewer").RequireClaim("permission", "tasks.read"))
            .AddPolicy("admin", policy => policy.RequireRole("Founder"));
        builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = identity));
        builder.Services.Configure<OpenIddictValidationSystemNetHttpOptions>(options => options.HttpResiliencePipeline = null);

        var app = builder.Build();
        app.UseHttp();
        if (cookieDefault)
        {
            app.MapGet("/cookie-login", async (HttpContext context) =>
            {
                var properties = new AuthenticationProperties();
                if (cookieToken)
                {
                    properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "cookie-token" }]);
                }
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", CookieUserId)], "Cookies")), properties);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }).AllowAnonymous();
        }
        app.MapMethods("/me", ["GET", "POST"], (ICurrentUser user) => new Profile(
            user.UserId,
            user.TryGetClaimValue<string>("sub", out var subject) ? subject : null,
            user.UserName,
            [.. user.Roles],
            user.HasPermission("tasks.read"),
            user.AccessToken)).RequireAuthorization();
        app.MapGet("/review", () => Results.Ok()).RequireAuthorization("review");
        app.MapGet("/admin", () => Results.Ok()).RequireAuthorization("admin");
        app.MapGet("/public", (ICurrentUser user) => new Profile(
            user.UserId, null, user.UserName, [.. user.Roles], false, user.AccessToken)).AllowAnonymous();
        app.MapPost("/json", (Payload payload) => TypedResults.Text(payload.Value)).RequireAuthorization();
        app.MapPost("/public-json", (Payload payload) => TypedResults.Text(payload.Value)).AllowAnonymous();
        app.MapPost("/form", ([FromForm] string value) => TypedResults.Text(value)).RequireAuthorization();
        app.MapPost("/admin-form", ([FromForm] string value) => TypedResults.Text(value)).RequireAuthorization("admin");
        app.MapPost("/form-optout", ([FromForm] string value) => TypedResults.Text(value))
            .RequireAuthorization()
            .DisableAntiforgery();
        app.MapGet("/antiforgery-token", (IAntiforgery antiforgery, HttpContext context) =>
            TypedResults.Text(antiforgery.GetAndStoreTokens(context).RequestToken!)).RequireAuthorization();

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app, string? token = null)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private sealed record Profile(
        Guid? UserId, string? Subject, string? Name, string[] Roles, bool CanRead, string? AccessToken);

    private sealed record Payload(string Value);
}
