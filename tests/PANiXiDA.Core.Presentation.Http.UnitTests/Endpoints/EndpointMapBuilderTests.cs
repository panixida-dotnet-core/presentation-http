using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointMapBuilderTests
{
    [Fact(DisplayName = "Constructor validates arguments")]
    public void Constructor_ShouldValidateArguments()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");

        var groupException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(null!, "/{id:guid}", "UpdateUser", "Updates a user."));
        var routeException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(group, null!, "UpdateUser", "Updates a user."));
        var nameException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(group, "/{id:guid}", null!, "Updates a user."));
        var summaryException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(group, "/{id:guid}", "UpdateUser", null!));

        groupException.ParamName.ShouldBe("group");
        routeException.ParamName.ShouldBe("route");
        nameException.ParamName.ShouldBe("name");
        summaryException.ParamName.ShouldBe("summary");
    }

    [Fact(DisplayName = "Constructor assigns properties")]
    public void Constructor_ShouldAssignProperties()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");

        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        endpointMapBuilder.Group.ShouldBeSameAs(group);
        endpointMapBuilder.Route.ShouldBe("/{id:guid}");
        endpointMapBuilder.Name.ShouldBe("UpdateUser");
        endpointMapBuilder.Summary.ShouldBe("Updates a user.");
    }

    [Fact(DisplayName = "MapGet maps the configured route, HTTP method, name, and summary")]
    public void MapGet_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapGet(static () => Results.Ok()),
            "GET");
    }

    [Fact(DisplayName = "MapPost maps the configured route, HTTP method, name, and summary")]
    public void MapPost_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPost(static () => Results.Created()),
            "POST");
    }

    [Fact(DisplayName = "MapPut maps the configured route, HTTP method, name, and summary")]
    public void MapPut_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPut(static () => Results.NoContent()),
            "PUT");
    }

    [Fact(DisplayName = "MapPatch maps the configured route, HTTP method, name, and summary")]
    public void MapPatch_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPatch(static () => Results.NoContent()),
            "PATCH");
    }

    [Fact(DisplayName = "MapDelete maps the configured route, HTTP method, name, and summary")]
    public void MapDelete_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapDelete(static () => Results.NoContent()),
            "DELETE");
    }

    [Fact(DisplayName = "MapMethods maps the configured route, HTTP methods, name, and summary")]
    public void MapMethods_ShouldMapConfiguredRouteHttpMethodsNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapMethods(["HEAD", "OPTIONS"], static () => Results.Ok()),
            "HEAD",
            "OPTIONS");
    }

    [Fact(DisplayName = "MapPut validates handler")]
    public void MapPut_ShouldValidateHandler()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");
        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        var exception = Should.Throw<ArgumentNullException>(() => endpointMapBuilder.MapPut(null!));

        exception.ParamName.ShouldBe("handler");
    }

    private static void AssertMappedEndpoint(
        Func<EndpointMapBuilder, RouteHandlerBuilder> mapEndpoint,
        params string[] expectedHttpMethods)
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");
        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        var result = mapEndpoint(endpointMapBuilder);

        result.ShouldNotBeNull();
        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        var httpMethodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();

        httpMethodMetadata.ShouldNotBeNull();
        httpMethodMetadata.HttpMethods.ShouldBe(expectedHttpMethods);
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("UpdateUser");
        endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Updates a user.");
    }

    private static EndpointMapBuilder CreateEndpointMapBuilder(RouteGroupBuilder group)
    {
        return new EndpointMapBuilder(group, "/{id:guid}", "UpdateUser", "Updates a user.");
    }

    private static RouteEndpoint GetRouteEndpoint(WebApplication app, string routePattern)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == routePattern);
    }
}
