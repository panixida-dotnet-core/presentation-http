using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.UnitTests.Support;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class HttpPipelineTests
{
    [Fact(DisplayName = "UseHttp supports a service provider without service registration introspection")]
    public async Task UseHttp_ShouldSupportServiceProviderWithoutIntrospection()
    {
        var builder = CreateBuilder();
        builder.Host.UseServiceProviderFactory(new ServiceProviderWithoutIntrospectionFactory());

        await using var app = builder.Build();
        app.UseHttp();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Theory(DisplayName = "UseHttp supports independently registered authentication and authorization services")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseHttp_ShouldSupportOptionalAuthenticationAndAuthorization(
        bool registerAuthentication,
        bool registerAuthorization)
    {
        var builder = CreateBuilder();
        if (registerAuthentication)
        {
            builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        }

        if (registerAuthorization)
        {
            builder.Services.AddAuthorization();
        }

        await using var app = builder.Build();
        app.UseHttp();
        app.MapGet("/public", (HttpContext context) => TypedResults.Text(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        if (registerAuthentication)
        {
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(app);
        }

        using var response = await client.GetAsync("/public", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe(registerAuthentication ? "user-id" : "anonymous");
    }

    [Theory(DisplayName = "UseHttp enforces endpoint policies and preserves authenticated route logging")]
    [InlineData(false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.OK)]
    public async Task UseHttp_ShouldAuthorizeBeforeRequestLogging(
        bool authenticated,
        bool hasPermission,
        HttpStatusCode expectedStatus)
    {
        var builder = CreateBuilder();
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("ReadOrders", policy => policy.RequireAuthenticatedUser().RequireClaim("permission", "orders.read"));
        var logger = new TestLogger<LoggingMiddleware>();
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);

        await using var app = builder.Build();
        app.UseHttp();
        app.MapGet("/orders/{id}", (HttpContext context) => TypedResults.Text(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)!))
            .WithDisplayName("Read order")
            .RequireAuthorization("ReadOrders");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(app, hasPermission);
        }

        using var response = await client.GetAsync("/orders/42", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("user-id");
            logger.Entries.ShouldHaveSingleItem();
            var scope = logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
                .Single(values => values.ContainsKey("http.route"));
            scope["http.route"].ShouldBe("/orders/{id}");
            scope["aspnetcore.endpoint.display_name"].ShouldBe("Read order");
            scope["enduser.id"].ShouldBe("user-id");
        }
        else
        {
            logger.Entries.ShouldBeEmpty();
        }
    }

    [Fact(DisplayName = "UseHttp handles authorization failures through Problem Details")]
    public async Task UseHttp_ShouldHandleAuthorizationExceptions()
    {
        var builder = CreateBuilder();
        builder.Services.AddAuthorizationBuilder().AddPolicy("Fail", policy => policy.RequireAssertion(
            bool (_) => throw new InvalidOperationException("Authorization failed")));

        await using var app = builder.Build();
        app.UseHttp();
        app.MapGet("/protected", () => TypedResults.Ok()).RequireAuthorization("Fail");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/protected", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("Internal server error");
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);
        return builder;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        return new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
    }

    private static AuthenticationHeaderValue CreateAuthorizationHeader(WebApplication app, bool hasPermission = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-id") };
        if (hasPermission)
        {
            claims.Add(new Claim("permission", "orders.read"));
        }

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) },
            BearerTokenDefaults.AuthenticationScheme);
        var options = app.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(BearerTokenDefaults.AuthenticationScheme);
        return new AuthenticationHeaderValue("Bearer", options.BearerTokenProtector.Protect(ticket));
    }

    private sealed class ServiceProviderWithoutIntrospectionFactory : IServiceProviderFactory<IServiceCollection>
    {
        public IServiceCollection CreateBuilder(IServiceCollection services) => services;

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            return new ServiceProviderWithoutIntrospection(containerBuilder.BuildServiceProvider());
        }
    }

    private sealed class ServiceProviderWithoutIntrospection(ServiceProvider services) : IServiceProvider, IAsyncDisposable
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IServiceProviderIsService) ? null : services.GetService(serviceType);
        }

        public ValueTask DisposeAsync() => services.DisposeAsync();
    }
}
