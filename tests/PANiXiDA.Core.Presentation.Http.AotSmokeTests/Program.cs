using Asp.Versioning;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using PANiXiDA.Core.Presentation.Http.AotSmokeTests;
using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.Modularity;

using System.Net;
using System.Text;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
var assembly = typeof(ProbeGroup).Assembly;
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    [$"HttpModules:{assembly.GetName().Name}:Name"] = "probe",
    [$"HttpModules:{assembly.GetName().Name}:Title"] = "AOT probe",
    ["ForwardedHeaders:ForwardLimit"] = "2",
    ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
    ["ForwardedHeaders:KnownIPNetworks:0:Prefix"] = "10.0.0.0",
    ["ForwardedHeaders:KnownIPNetworks:0:PrefixLength"] = "8",
    ["ScalarConfiguration:Title"] = "AOT probe"
});

// AddHttp intentionally retains the dependency-bound MVC ApiExplorer path.
// Exercise the library's generated registration, configuration and HTTP behavior independently.
var modules = new HttpModuleRegistry(builder.Configuration, [assembly]);
builder.Services.AddSingleton(modules);
builder.Services.AddSingleton(new ProbeDependency("generated"));
builder.Services.AddForwardedHeadersConfiguration(builder.Configuration);
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});
builder.Services.AddJsonConfiguration();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ProbeJsonContext.Default));
builder.Services.AddOpenApiConfiguration(builder.Configuration, modules.Modules);
builder.Services.AddProblemDetailsConfiguration();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddHealthChecks();

await using var app = builder.Build();
app.UseHttp(assembly);
app.MapOpenApi();
await app.StartAsync();
try
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    await AssertResponse(client, "/health", HttpStatusCode.OK, "Healthy");
    await AssertResponse(client, "/api/v1/probe/42", HttpStatusCode.OK, "generated");
    await AssertResponse(client, "/api/v1/probe/error", HttpStatusCode.InternalServerError, "traceId");
    await AssertResponse(client, "/openapi/probe.json", HttpStatusCode.OK, "ProbeGet");
    using var invalid = new StringContent("{\"name\":\"\"}", Encoding.UTF8, "application/json");
    using var validation = await client.PostAsync("/api/v1/probe", invalid);
    if (validation.StatusCode != HttpStatusCode.BadRequest)
    {
        throw new InvalidOperationException($"Expected generated validation to return 400, got {(int)validation.StatusCode}.");
    }

    Console.WriteLine("PASS: generated discovery, constructor injection, configuration, HTTP routing, JSON, validation, ProblemDetails and OpenAPI.");
}
finally
{
    await app.StopAsync();
}

static async Task AssertResponse(HttpClient client, string path, HttpStatusCode expectedStatus, string expectedContent)
{
    using var response = await client.GetAsync(path);
    var content = await response.Content.ReadAsStringAsync();
    if (response.StatusCode != expectedStatus || !content.Contains(expectedContent, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Unexpected response for {path}: {(int)response.StatusCode} {content}");
    }
}
