using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;

using System.Net;
using System.Text;
using System.Text.Json;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class JsonConfigurationTests
{
    [Theory(DisplayName = "AddHttp enables strict JSON contracts for both registration overloads")]
    [InlineData(false)]
    [InlineData(true)]
    public void AddHttp_ShouldEnableStrictJsonContracts(bool useModule)
    {
        var services = new ServiceCollection();
        var moduleAssembly = typeof(JsonConfigurationTests).Assembly;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"HttpModules:{moduleAssembly.GetName().Name}:Name"] = "tests",
                [$"HttpModules:{moduleAssembly.GetName().Name}:Title"] = "Test API"
            })
            .Build();

        if (useModule)
        {
            services.AddHttp(configuration, moduleAssembly);
        }
        else
        {
            services.AddHttp(configuration);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;

        options.SerializerOptions.RespectRequiredConstructorParameters.ShouldBeTrue();
        options.SerializerOptions.RespectNullableAnnotations.ShouldBeTrue();
    }

    [Theory(DisplayName = "UseHttp rejects missing constructor parameters and null non-nullable values")]
    [InlineData("""{"count":1,"requiredNote":null}""")]
    [InlineData("""{"name":"Melee","requiredNote":null}""")]
    [InlineData("""{"name":"Melee","count":1}""")]
    [InlineData("""{"name":null,"count":1,"requiredNote":null}""")]
    [InlineData("""{"name":"Melee","count":1,"requiredNote":null,"description":null}""")]
    public async Task UseHttp_ShouldRejectInvalidJsonContract(string payload)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await CreateStartedApplicationAsync(cancellationToken);
        using var client = CreateClient(app);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/requests", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(responseBody);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Theory(DisplayName = "UseHttp accepts nullable values and preserves optional constructor defaults")]
    [InlineData("""{"name":"Melee","count":1,"requiredNote":null}""", null)]
    [InlineData("""{"name":"Melee","count":1,"requiredNote":null,"shots":null}""", null)]
    [InlineData("""{"name":"Archer","count":1,"requiredNote":"note","shots":12}""", 12)]
    public async Task UseHttp_ShouldAcceptValidJsonContract(string payload, int? expectedShots)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await CreateStartedApplicationAsync(cancellationToken);
        using var client = CreateClient(app);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/requests", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
        var request = JsonSerializer.Deserialize<ContractRequest>(responseBody, JsonSerializerOptions.Web);
        request.ShouldNotBeNull();
        request.Shots.ShouldBe(expectedShots);
        request.Description.ShouldBe("Default description");
    }

    [Fact(DisplayName = "UseHttp rejects null non-nullable init properties")]
    public async Task UseHttp_ShouldRejectNullNonNullableProperty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await CreateStartedApplicationAsync(cancellationToken);
        using var client = CreateClient(app);
        using var content = new StringContent("""{"name":null}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/property-requests", content, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "AddHttp rejects null non-nullable response properties during serialization")]
    public void AddHttp_ShouldRejectNullNonNullableResponseProperties()
    {
        var services = new ServiceCollection();
        services.AddHttp(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;
        var response = new ResponsePayload(null!);

        Should.Throw<JsonException>(() =>
            JsonSerializer.Serialize(response, options.SerializerOptions));
    }

    private static async Task<WebApplication> CreateStartedApplicationAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttp(builder.Configuration);

        var app = builder.Build();
        app.UseHttp();
        app.MapPost("/requests", static (ContractRequest request) => TypedResults.Ok(request));
        app.MapPost("/property-requests", static (PropertyRequest request) => TypedResults.Ok(request));
        await app.StartAsync(cancellationToken);

        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server addresses feature is not available.");

        return new HttpClient { BaseAddress = new Uri(addresses.Addresses.Single()) };
    }

    private sealed record ContractRequest(
        string Name,
        int Count,
        string? RequiredNote,
        int? Shots = null,
        string Description = "Default description");

    private sealed record PropertyRequest
    {
        public string Name { get; init; } = "Default name";
    }

    private sealed record ResponsePayload(string Name);
}
