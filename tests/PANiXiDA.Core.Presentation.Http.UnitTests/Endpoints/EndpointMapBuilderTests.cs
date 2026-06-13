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

    [Fact(DisplayName = "MapPut maps the configured route, HTTP method, name, and summary")]
    public void MapPut_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");
        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        var result = endpointMapBuilder.MapPut(static () => Results.NoContent());

        result.ShouldNotBeNull();
        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        var httpMethodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();

        httpMethodMetadata.ShouldNotBeNull();
        httpMethodMetadata.HttpMethods.ShouldBe(["PUT"]);
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("UpdateUser");
        endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Updates a user.");
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
