using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
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
using System.Text.Encodings.Web;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class HttpPipelineTests
{
    [Fact(DisplayName = "UseHttp supports a service provider without service registration introspection")]
    public async Task UseHttp_ShouldSupportServiceProviderWithoutIntrospection()
    {
        var builder = CreateBuilder(supportsIntrospection: false);

        await using var app = builder.Build();
        app.UseHttp();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Theory(DisplayName = "UseHttp supports both default and explicit host authentication and authorization registration")]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public async Task UseHttp_ShouldSupportHostAuthenticationAndAuthorizationRegistration(
        bool registerAuthentication,
        bool registerAuthorization,
        bool supportsIntrospection)
    {
        var builder = CreateBuilder(supportsIntrospection);
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
    public async Task UseHttp_ShouldLogFinalAuthorizationResult(
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
        var completed = ObserveCompletion(app);
        app.UseHttp();
        app.MapGet("/orders/{id}", (int id, HttpContext context) => TypedResults.Text(
                $"{id}:{context.User.FindFirstValue(ClaimTypes.NameIdentifier)}"))
            .WithDisplayName("Read order")
            .RequireAuthorization("ReadOrders");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(app, hasPermission);
        }

        using var response = await client.GetAsync("/orders/42", TestContext.Current.CancellationToken);
        await completed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("42:user-id");
        }

        var completion = logger.Entries.ShouldHaveSingleItem();
        completion.LogLevel.ShouldBe(expectedStatus == HttpStatusCode.OK ? LogLevel.Information : LogLevel.Warning);
        var scope = logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.route"));
        scope["http.route"].ShouldBe("/orders/{id}");
        scope["aspnetcore.endpoint.display_name"].ShouldBe("Read order");
        logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("enduser.id"))["enduser.id"]
            .ShouldBe(authenticated ? "subject-id" : null);
        logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.response.status_code"))["http.response.status_code"]
            .ShouldBe((int)expectedStatus);
    }

    [Theory(DisplayName = "UseHttp logs authentication and authorization failures once with the final status and route")]
    [InlineData(true, true, FailureKind.BadRequest, 400, LogLevel.Warning)]
    [InlineData(true, false, FailureKind.BadRequest, 400, LogLevel.Warning)]
    [InlineData(false, true, FailureKind.BadRequest, 400, LogLevel.Warning)]
    [InlineData(false, false, FailureKind.BadRequest, 400, LogLevel.Warning)]
    [InlineData(true, true, FailureKind.ClientCancellation, 499, LogLevel.Warning)]
    [InlineData(true, false, FailureKind.ClientCancellation, 499, LogLevel.Warning)]
    [InlineData(false, true, FailureKind.ClientCancellation, 499, LogLevel.Warning)]
    [InlineData(false, false, FailureKind.ClientCancellation, 499, LogLevel.Warning)]
    [InlineData(true, true, FailureKind.CancellationAggregate, 499, LogLevel.Warning)]
    [InlineData(true, false, FailureKind.CancellationAggregate, 499, LogLevel.Warning)]
    [InlineData(false, true, FailureKind.CancellationAggregate, 499, LogLevel.Warning)]
    [InlineData(false, false, FailureKind.CancellationAggregate, 499, LogLevel.Warning)]
    [InlineData(true, true, FailureKind.IndependentCancellation, 500, LogLevel.Error)]
    [InlineData(true, false, FailureKind.IndependentCancellation, 500, LogLevel.Error)]
    [InlineData(false, true, FailureKind.IndependentCancellation, 500, LogLevel.Error)]
    [InlineData(false, false, FailureKind.IndependentCancellation, 500, LogLevel.Error)]
    [InlineData(true, true, FailureKind.ApplicationFailure, 500, LogLevel.Error)]
    [InlineData(true, false, FailureKind.ApplicationFailure, 500, LogLevel.Error)]
    [InlineData(false, true, FailureKind.ApplicationFailure, 500, LogLevel.Error)]
    [InlineData(false, false, FailureKind.ApplicationFailure, 500, LogLevel.Error)]
    public async Task UseHttp_ShouldLogAuthenticationAndAuthorizationFailures(
        bool failAuthentication,
        bool supportsIntrospection,
        FailureKind failureKind,
        int expectedStatus,
        LogLevel expectedLevel)
    {
        var builder = CreateBuilder(supportsIntrospection);
        var requestAborted = new CancellationToken(failureKind is FailureKind.ClientCancellation or FailureKind.CancellationAggregate or FailureKind.ApplicationFailure);
        Exception exception = failureKind switch
        {
            FailureKind.BadRequest => new BadHttpRequestException("Unexpected end of request content."),
            FailureKind.CancellationAggregate => new AggregateException(new TaskCanceledException("Request cancelled", null, requestAborted)),
            FailureKind.ApplicationFailure => new InvalidOperationException("Independent authentication failure"),
            _ => new TaskCanceledException("Request cancelled", null, requestAborted)
        };
        var logger = new TestLogger<LoggingMiddleware>();
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);
        if (failAuthentication)
        {
            builder.Services.AddSingleton(new RequestFailure(exception, requestAborted));
            builder.Services.AddAuthentication("Failure")
                .AddScheme<AuthenticationSchemeOptions, FailingAuthenticationHandler>("Failure", null);
        }
        else
        {
            builder.Services.AddAuthorizationBuilder().AddPolicy("Fail", policy => policy.RequireAssertion(bool (context) =>
            {
                ((HttpContext)context.Resource!).RequestAborted = requestAborted;
                throw exception;
            }));
        }

        await using var app = builder.Build();
        var completed = ObserveCompletion(app);
        app.UseHttp();
        var endpoint = app.MapGet("/protected", () => TypedResults.Ok()).WithDisplayName("Protected endpoint");
        if (!failAuthentication)
        {
            endpoint.RequireAuthorization("Fail");
        }
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/protected", TestContext.Current.CancellationToken);
        await completed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)expectedStatus);
        var completion = logger.Entries.ShouldHaveSingleItem();
        completion.LogLevel.ShouldBe(expectedLevel);
        completion.Exception.ShouldBeSameAs(failureKind == FailureKind.ClientCancellation ? null : exception);
        var scope = logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.route"));
        scope["http.route"].ShouldBe("/protected");
        scope["aspnetcore.endpoint.display_name"].ShouldBe("Protected endpoint");
        logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.response.status_code"))["http.response.status_code"]
            .ShouldBe(expectedStatus);
        if (expectedStatus != StatusCodes.Status499ClientClosedRequest)
        {
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Theory(DisplayName = "UseHttp applies default and endpoint CORS before authorization")]
    [InlineData(false, false, false, true, HttpStatusCode.Unauthorized)]
    [InlineData(false, false, true, true, HttpStatusCode.Forbidden)]
    [InlineData(false, true, false, true, HttpStatusCode.NoContent)]
    [InlineData(true, false, false, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, true, true, HttpStatusCode.Forbidden)]
    [InlineData(true, true, false, true, HttpStatusCode.NoContent)]
    [InlineData(false, false, false, false, HttpStatusCode.Unauthorized)]
    [InlineData(false, false, true, false, HttpStatusCode.Forbidden)]
    [InlineData(false, true, false, false, HttpStatusCode.NoContent)]
    [InlineData(true, false, false, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, true, false, HttpStatusCode.Forbidden)]
    [InlineData(true, true, false, false, HttpStatusCode.NoContent)]
    public async Task UseHttp_ShouldApplyCorsBeforeAuthorization(
        bool useDefaultPolicy,
        bool preflight,
        bool authenticated,
        bool supportsIntrospection,
        HttpStatusCode expectedStatus)
    {
        const string origin = "https://admin.example";
        var builder = CreateBuilder(supportsIntrospection);
        var logger = new TestLogger<LoggingMiddleware>();
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);
        builder.Services.AddCors(options =>
        {
            options.DefaultPolicyName = "DefaultBrowser";
            options.AddPolicy(
                useDefaultPolicy ? options.DefaultPolicyName : "Browser",
                policy => policy.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod());
        });
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorizationBuilder().AddPolicy("Admin", policy => policy.RequireRole("Admin"));

        await using var app = builder.Build();
        var completed = ObserveCompletion(app);
        app.UseHttp();
        var endpoint = app.MapGet("/protected", () => TypedResults.Ok()).RequireAuthorization("Admin");
        if (!useDefaultPolicy)
        {
            endpoint.RequireCors("Browser");
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(preflight ? HttpMethod.Options : HttpMethod.Get, "/protected");
        request.Headers.Add("Origin", origin);
        if (preflight)
        {
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "Authorization");
        }

        if (authenticated)
        {
            request.Headers.Authorization = CreateAuthorizationHeader(app);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await completed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expectedStatus);
        logger.Entries.ShouldHaveSingleItem().LogLevel.ShouldBe(preflight ? LogLevel.Information : LogLevel.Warning);
        var scope = logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.route"));
        scope["http.route"].ShouldBe(useDefaultPolicy && preflight ? null : "/protected");
        logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("url.path"))["url.path"].ShouldBe("/protected");
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([origin]);
        if (preflight)
        {
            response.Headers.GetValues("Access-Control-Allow-Methods").ShouldBe(["GET"]);
            response.Headers.GetValues("Access-Control-Allow-Headers").ShouldBe(["Authorization"]);
        }
    }

    [Theory(DisplayName = "UseHttp does not emit CORS headers without a configured policy")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseHttp_ShouldNotEnableCorsWithoutPolicy(bool registerCors, bool supportsIntrospection)
    {
        var builder = CreateBuilder(supportsIntrospection);
        if (registerCors)
        {
            builder.Services.AddCors();
        }

        await using var app = builder.Build();
        app.UseHttp();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://admin.example");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Theory(DisplayName = "UseHttp enforces fallback authorization and allows anonymous endpoints with either service provider")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UseHttp_ShouldEnforceFallbackAuthorization(bool authenticated, bool supportsIntrospection)
    {
        var builder = CreateBuilder(supportsIntrospection);
        builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddScoped<IAuthorizationHandler, PassThroughAuthorizationHandler>();

        await using var app = builder.Build();
        app.UseHttp();
        app.MapGet("/protected", (HttpContext context) => TypedResults.Text(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"));
        app.MapGet("/public", () => TypedResults.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient(app);
        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization = CreateAuthorizationHeader(app);
        }

        using var protectedResponse = await client.GetAsync("/protected", TestContext.Current.CancellationToken);
        using var publicResponse = await client.GetAsync("/public", TestContext.Current.CancellationToken);

        protectedResponse.StatusCode.ShouldBe(authenticated ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        if (authenticated)
        {
            (await protectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("user-id");
        }

        publicResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "UseHttp logs redirects and short-circuited endpoints with the final status")]
    [InlineData(204, LogLevel.Information)]
    [InlineData(307, LogLevel.Information)]
    [InlineData(400, LogLevel.Warning)]
    [InlineData(499, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    public async Task UseHttp_ShouldLogShortCircuitedRequests(int expectedStatus, LogLevel expectedLevel)
    {
        var builder = CreateBuilder();
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 8443);
        var logger = new TestLogger<LoggingMiddleware>();
        builder.Services.AddSingleton<ILogger<LoggingMiddleware>>(logger);

        await using var app = builder.Build();
        var completed = ObserveCompletion(app);
        app.UseHttp();
        app.Use((context, next) =>
        {
            context.Response.Headers["X-After-Routing"] = "executed";
            return next(context);
        });
        app.MapGet("/short", (HttpContext context) =>
        {
            if (expectedStatus == 400)
            {
                throw new BadHttpRequestException("Incomplete request body");
            }
            if (expectedStatus == 499)
            {
                context.RequestAborted = new CancellationToken(true);
                throw new TaskCanceledException("Client aborted", null, context.RequestAborted);
            }
            if (expectedStatus == 500)
            {
                throw new InvalidOperationException("Independent failure");
            }
            return TypedResults.NoContent();
        }).WithDisplayName("Short endpoint").ShortCircuit();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
        if (expectedStatus != 307)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        }

        using var response = await client.GetAsync("/short", TestContext.Current.CancellationToken);
        await completed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe((HttpStatusCode)expectedStatus);
        response.Headers.Contains("X-After-Routing").ShouldBeFalse();
        logger.Entries.ShouldHaveSingleItem().LogLevel.ShouldBe(expectedLevel);
        var scope = logger.Scopes.OfType<IReadOnlyDictionary<string, object?>>()
            .Single(values => values.ContainsKey("http.route"));
        scope["http.route"].ShouldBe(expectedStatus == 307 ? null : "/short");
        scope["aspnetcore.endpoint.display_name"].ShouldBe(expectedStatus == 307 ? null : "Short endpoint");
    }

    public enum FailureKind
    {
        BadRequest,
        ClientCancellation,
        CancellationAggregate,
        IndependentCancellation,
        ApplicationFailure
    }

    private sealed record RequestFailure(Exception Exception, CancellationToken RequestAborted);

    private sealed class FailingAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        RequestFailure failure) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Context.RequestAborted = failure.RequestAborted;
            return Task.FromException<AuthenticateResult>(failure.Exception);
        }
    }

    private static Task ObserveCompletion(WebApplication app)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Use(async (context, next) =>
        {
            await next(context);
            completed.SetResult();
        });
        return completed.Task;
    }

    private static WebApplicationBuilder CreateBuilder(bool supportsIntrospection = true)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);
        if (!supportsIntrospection)
        {
            builder.Host.UseServiceProviderFactory(new ServiceProviderWithoutIntrospectionFactory());
        }

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
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-id"), new("sub", "subject-id") };
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
            return new ServiceProviderWithoutIntrospection(containerBuilder.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
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
