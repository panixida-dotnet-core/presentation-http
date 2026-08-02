using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddHttp registers HTTP infrastructure and returns the same service collection")]
    public void AddHttp_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddHttp(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "AddHttp applies ForwardedHeaders configuration")]
    public void AddHttp_ShouldApplyForwardedHeadersConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(ForwardedHeadersOptions.ForwardLimit)] = "4"
            })
            .Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.ShouldBe(4);
    }

    [Fact(DisplayName = "AddHttp applies ForwardedHeaders section from root configuration")]
    public void AddHttp_ShouldApplyForwardedHeadersSectionFromRootConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:" + nameof(ForwardedHeadersOptions.ForwardLimit)] = "6"
            })
            .Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.ShouldBe(6);
    }

    [Fact(DisplayName = "AddHttp registers health check services")]
    public void AddHttp_ShouldRegisterHealthCheckServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        healthCheckService.ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddHttp uses default settings when configuration is empty")]
    public void AddHttp_ShouldUseDefaultSettingsWhenConfigurationIsEmpty()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddHttp(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact(DisplayName = "AddHttp uses strict JSON number handling")]
    public void AddHttp_ShouldUseStrictJsonNumberHandling()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;

        options.SerializerOptions.NumberHandling.ShouldBe(JsonNumberHandling.Strict);
    }

    [Fact(DisplayName = "AddHttp rejects JSON numbers encoded as strings")]
    public void AddHttp_ShouldRejectJsonNumbersEncodedAsStrings()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;

        Should.Throw<JsonException>(() =>
        {
            JsonSerializer.Deserialize<TestPayload>(
                """{"count":"1","status":"Active"}""",
                options.SerializerOptions);
        });
    }

    [Fact(DisplayName = "AddHttp preserves string enum deserialization")]
    public void AddHttp_ShouldPreserveStringEnumDeserialization()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var payload = JsonSerializer.Deserialize<TestPayload>(
            """{"count":1,"status":"Active"}""",
            options.SerializerOptions);

        payload.ShouldNotBeNull();
        payload.Count.ShouldBe(1);
        payload.Status.ShouldBe(TestStatus.Active);
    }

    [Fact(DisplayName = "AddHttp rejects a null module assembly collection")]
    public void AddHttp_ShouldRejectNullModuleAssemblyCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        Assembly[] moduleAssemblies = null!;

        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            services.AddHttp(configuration, moduleAssemblies);
        });

        exception.ParamName.ShouldBe("moduleAssemblies");
    }

    [Fact(DisplayName = "AddHttp rejects null values in the module assembly collection")]
    public void AddHttp_ShouldRejectNullModuleAssembly()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        Assembly[] moduleAssemblies = [null!];

        var exception = Should.Throw<ArgumentException>(() =>
        {
            services.AddHttp(configuration, moduleAssemblies);
        });

        exception.ParamName.ShouldBe("presentationAssemblies");
        exception.Message.ShouldContain("cannot contain null values");
    }

    [Fact(DisplayName = "AddHttp rejects duplicate module document names")]
    public void AddHttp_ShouldRejectDuplicateModuleDocumentNames()
    {
        var services = new ServiceCollection();
        var firstAssembly = typeof(ADiscoveredEndpointGroup).Assembly;
        var secondAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateModuleConfigurationValues(
                (firstAssembly, "identity", "Identity API"),
                (secondAssembly, "IDENTITY", "Duplicate Identity API")))
            .Build();

        var exception = Should.Throw<ArgumentException>(() =>
        {
            services.AddHttp(configuration, firstAssembly, secondAssembly);
        });

        exception.ParamName.ShouldBe("presentationAssemblies");
        exception.Message.ShouldContain("IDENTITY");
    }

    [Fact(DisplayName = "AddHttp rejects a presentation assembly assigned to multiple modules")]
    public void AddHttp_ShouldRejectDuplicateModuleAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(ADiscoveredEndpointGroup).Assembly;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateModuleConfigurationValues(
                (assembly, "identity", "Identity API")))
            .Build();

        var exception = Should.Throw<ArgumentException>(() =>
        {
            services.AddHttp(configuration, assembly, assembly);
        });

        exception.ParamName.ShouldBe("presentationAssemblies");
        exception.Message.ShouldContain(assembly.FullName!);
    }

    [Fact(DisplayName = "UseHttp adds middleware and maps groups from the provided assemblies")]
    public void UseHttp_ShouldReturnSameApplicationAndMapEndpointGroups()
    {
        EndpointMappingRecorder.Clear();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddHttp(builder.Configuration);

        using var app = builder.Build();

        var result = app.UseHttp(typeof(ADiscoveredEndpointGroup).Assembly);

        result.ShouldBeSameAs(app);
        EndpointMappingRecorder.Entries.ShouldContain(nameof(ADiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.ShouldContain(nameof(BDiscoveredEndpointGroup));
    }

    [Fact(DisplayName = "UseHttp maps a registered module assembly only once")]
    public void UseHttp_ShouldMapRegisteredHttpModuleAssemblyOnce()
    {
        EndpointMappingRecorder.Clear();

        var moduleAssembly = typeof(ADiscoveredEndpointGroup).Assembly;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(CreateModuleConfigurationValues(
            (moduleAssembly, "tests", "Test endpoints")));

        builder.Services.AddHttp(builder.Configuration, moduleAssembly);

        using var app = builder.Build();

        var result = app.UseHttp(moduleAssembly);

        result.ShouldBeSameAs(app);
        EndpointMappingRecorder.Entries.Count(
            entry => entry == nameof(ADiscoveredEndpointGroup)).ShouldBe(1);
        EndpointMappingRecorder.Entries.Count(
            entry => entry == nameof(BDiscoveredEndpointGroup)).ShouldBe(1);
    }

    [Fact(DisplayName = "UseHttp uses ProblemDetails exception handler in Development")]
    public async Task UseHttp_ShouldUseProblemDetailsExceptionHandlerInDevelopment()
    {
        await using var app = await CreateStartedThrowingApplicationAsync(
            Environments.Development,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        content.ShouldContain("Internal server error");
        content.ShouldContain("Pipeline development failure");
        content.ShouldNotContain(nameof(InvalidOperationException));
    }

    [Fact(DisplayName = "UseHttp returns Bad Request when JSON body binding fails in Development")]
    public async Task UseHttp_ShouldReturnBadRequestWhenJsonBodyBindingFailsInDevelopment()
    {
        await using var app = await CreateStartedApplicationAsync(
            Environments.Development,
            TestContext.Current.CancellationToken,
            static application =>
            {
                application.MapPost(
                    "/payload",
                    static (RequestPayload payload) => Results.Ok(payload));
            });
        using var client = CreateClient(app);
        using var requestContent = new StringContent(
            """{"id":"invalid-guid"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/payload",
            requestContent,
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        content.ShouldContain("Bad Request");
        content.ShouldContain("Failed to read parameter");
        content.ShouldNotContain("Internal server error");
    }

    [Fact(DisplayName = "UseHttp uses ProblemDetails exception handler outside Development")]
    public async Task UseHttp_ShouldUseProblemDetailsExceptionHandlerOutsideDevelopment()
    {
        await using var app = await CreateStartedThrowingApplicationAsync(
            Environments.Production,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        content.ShouldContain("Internal server error");
        content.ShouldNotContain("Pipeline development failure");
    }

    [Fact(DisplayName = "UseHttp maps the health check endpoint")]
    public async Task UseHttp_ShouldMapHealthCheckEndpoint()
    {
        await using var app = await CreateStartedApplicationAsync(
            Environments.Production,
            TestContext.Current.CancellationToken);
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldBe("Healthy");
    }

    private static async Task<WebApplication> CreateStartedThrowingApplicationAsync(
        string environmentName,
        CancellationToken cancellationToken)
    {
        return await CreateStartedApplicationAsync(environmentName, cancellationToken, app =>
        {
            app.MapGet("/throw", static () =>
            {
                throw new InvalidOperationException("Pipeline development failure");
            });
        });
    }

    private static async Task<WebApplication> CreateStartedApplicationAsync(
        string environmentName,
        CancellationToken cancellationToken,
        Action<WebApplication>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);

        var app = builder.Build();
        app.UseHttp();
        configure?.Invoke(app);

        await app.StartAsync(cancellationToken);

        return app;
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

    private sealed record TestPayload(int Count, TestStatus Status);

    private sealed record RequestPayload(Guid Id);

    [JsonConverter(typeof(JsonStringEnumConverter<TestStatus>))]
    private enum TestStatus
    {
        Active
    }
}
