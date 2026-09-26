using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PANiXiDA.Core.Application.Querying.Sorting;
using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.Modularity;

using System.Net;
using System.Text.Json;
using SortDirection = PANiXiDA.Core.Application.Querying.Sorting.SortDirection;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class SortingOpenApiTests
{
    [Theory(DisplayName = "OpenAPI describes sorting as optional repeated strings in default and module documents")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenApi_WhenSortingIsBound_DescribesWireFormat(bool useModule)
    {
        await using var app = await CreateApplicationAsync(useModule);
        using var client = CreateClient(app);
        var documentName = useModule ? "users" : "v1";

        var content = await client.GetStringAsync($"/openapi/{documentName}.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);

        AssertSortingParameter(GetParameter(document, "/sorting", "Fields"));
        AssertSortingParameter(GetParameter(document, "/renamed", "sort"));

        var other = GetParameter(document, "/other", "Fields");
        other.GetProperty("schema").GetProperty("items").GetProperty("type").GetString().ShouldBe("integer");
        other.TryGetProperty("description", out _).ShouldBeFalse();

        var header = GetParameter(document, "/headers", "Fields");
        header.GetProperty("in").GetString().ShouldBe("header");
        header.GetProperty("schema").GetProperty("items").TryGetProperty("$ref", out _).ShouldBeTrue();

        var body = document.RootElement.GetProperty("paths").GetProperty("/body").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        body.GetProperty("items").TryGetProperty("$ref", out _).ShouldBeTrue();
        var response = document.RootElement.GetProperty("paths").GetProperty("/sorting").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        response.GetProperty("items").TryGetProperty("$ref", out _).ShouldBeTrue();
    }

    [Theory(DisplayName = "AsParameters binds repeated sort criteria and rejects malformed values")]
    [InlineData("/sorting", HttpStatusCode.OK, 0)]
    [InlineData("/sorting?Fields=name:desc&Fields=department.name", HttpStatusCode.OK, 2)]
    [InlineData("/renamed?sort=name:desc&sort=department.name", HttpStatusCode.OK, 2)]
    [InlineData("/sorting?Fields=name:wrong", HttpStatusCode.BadRequest, 0)]
    [InlineData("/sorting?Fields=name:desc,department.name:asc", HttpStatusCode.BadRequest, 0)]
    public async Task Request_WhenSortingIsProvided_BindsWireFormat(
        string path,
        HttpStatusCode status,
        int count)
    {
        await using var app = await CreateApplicationAsync(false);
        using var client = CreateClient(app);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        if (status == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(content);
            var fields = document.RootElement;
            fields.GetArrayLength().ShouldBe(count);
            if (count > 0)
            {
                fields[0].GetProperty("field").GetString().ShouldBe("name");
                fields[0].GetProperty("order").GetInt32().ShouldBe((int)SortDirection.Desc);
                fields[1].GetProperty("field").GetString().ShouldBe("department.name");
                fields[1].GetProperty("order").GetInt32().ShouldBe((int)SortDirection.Asc);
            }
        }
    }

    private static void AssertSortingParameter(JsonElement parameter)
    {
        parameter.GetProperty("in").GetString().ShouldBe("query");
        (parameter.TryGetProperty("required", out var required) && required.GetBoolean()).ShouldBeFalse();
        (parameter.TryGetProperty("style", out var style) ? style.GetString() : "form").ShouldBe("form");
        (!parameter.TryGetProperty("explode", out var explode) || explode.GetBoolean()).ShouldBeTrue();
        parameter.GetProperty("schema").GetProperty("type").GetString().ShouldBe("array");
        parameter.GetProperty("schema").GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
        var description = parameter.GetProperty("description").GetString();
        description.ShouldNotBeNull();
        description.ShouldContain("field[:asc|desc]");
    }

    private static JsonElement GetParameter(
        JsonDocument document,
        string path,
        string name)
    {
        return document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get")
            .GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == name);
    }

    private static async Task<WebApplication> CreateApplicationAsync(bool useModule)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var module = new HttpModule("users", "Users", typeof(SortingOpenApiTests).Assembly);
        builder.Services.AddJsonConfiguration();
        builder.Services.AddOpenApiConfiguration(builder.Configuration, useModule ? [module] : []);
        var app = builder.Build();
        app.MapGet("/sorting", ([AsParameters] SortingParameters sorting) => TypedResults.Ok(sorting.Fields)).WithMetadata(module);
        app.MapGet("/renamed", ([FromQuery(Name = "sort")] SortField[] fields) => TypedResults.Ok(fields)).WithMetadata(module);
        app.MapGet("/other", ([AsParameters] OtherParameters parameters) => TypedResults.Ok(parameters.Fields)).WithMetadata(module);
        app.MapGet("/headers", ([FromHeader(Name = "Fields")] SortField[] fields) => TypedResults.Ok(fields)).WithMetadata(module);
        app.MapPost("/body", ([FromBody] SortField[] fields) => TypedResults.Ok(fields)).WithMetadata(module);
        app.UseOpenApiConfiguration();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private sealed record OtherParameters(int[] Fields);
}
