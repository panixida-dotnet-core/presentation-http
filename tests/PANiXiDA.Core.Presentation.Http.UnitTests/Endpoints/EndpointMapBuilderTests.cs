using Microsoft.AspNetCore.Authorization;
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

        var groupException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(
            null!,
            "/{id:guid}",
            "UpdateUser",
            "Updates a user."));
        var routeException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(
            group,
            null!,
            "UpdateUser",
            "Updates a user."));
        var nameException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(
            group,
            "/{id:guid}",
            null!,
            "Updates a user."));
        var summaryException = Should.Throw<ArgumentNullException>(() => new EndpointMapBuilder(
            group,
            "/{id:guid}",
            "UpdateUser",
            null!));

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
            static builder => builder.MapGet(
                builder.Route,
                static () => Results.Ok()),
            "GET");
    }

    [Fact(DisplayName = "MapPost maps the configured route, HTTP method, name, and summary")]
    public void MapPost_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPost(
                builder.Route,
                static () => Results.Created()),
            "POST");
    }

    [Fact(DisplayName = "MapPut maps the configured route, HTTP method, name, and summary")]
    public void MapPut_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPut(
                builder.Route,
                static () => Results.NoContent()),
            "PUT");
    }

    [Fact(DisplayName = "MapPatch maps the configured route, HTTP method, name, and summary")]
    public void MapPatch_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapPatch(
                builder.Route,
                static () => Results.NoContent()),
            "PATCH");
    }

    [Fact(DisplayName = "MapDelete maps the configured route, HTTP method, name, and summary")]
    public void MapDelete_ShouldMapConfiguredRouteHttpMethodNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapDelete(
                builder.Route,
                static () => Results.NoContent()),
            "DELETE");
    }

    [Fact(DisplayName = "MapMethods maps the configured route, HTTP methods, name, and summary")]
    public void MapMethods_ShouldMapConfiguredRouteHttpMethodsNameAndSummary()
    {
        AssertMappedEndpoint(
            static builder => builder.MapMethods(
                builder.Route,
                ["HEAD", "OPTIONS"],
                static () => Results.Ok()),
            "HEAD",
            "OPTIONS");
    }

    [Fact(DisplayName = "MapGet isolates endpoint metadata from sibling routes")]
    public void MapGet_ShouldIsolateMetadataFromSiblingRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");
        var first = new EndpointMapBuilder(
            group,
            "/first",
            "First",
            "First summary");
        var second = new EndpointMapBuilder(
            group,
            "/second",
            "Second",
            "Second summary");

        first.MapGet(first.Route, Handle);
        second.MapGet(second.Route, Handle);
        group.MapGet("/sibling", Handle);

        var firstEndpoint = GetRouteEndpoint(app, "/users/first");
        var secondEndpoint = GetRouteEndpoint(app, "/users/second");
        var siblingEndpoint = GetRouteEndpoint(app, "/users/sibling");
        firstEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("First");
        firstEndpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("First summary");
        secondEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("Second");
        secondEndpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Second summary");
        siblingEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>().ShouldBeNull();
        siblingEndpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>().ShouldBeNull();
    }

    [Fact(DisplayName = "MapGet preserves parent authorization and explicit endpoint conventions")]
    public void MapGet_ShouldPreserveParentAndEndpointConventions()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users").RequireAuthorization("group-policy");
        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        endpointMapBuilder.MapGet(endpointMapBuilder.Route, Handle)
            .WithName("ExplicitName")
            .WithSummary("Explicit summary")
            .RequireAuthorization("endpoint-policy");

        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("ExplicitName");
        endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Explicit summary");
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(static metadata => metadata.Policy)
            .ShouldBe(["group-policy", "endpoint-policy"]);
    }

    [Fact(DisplayName = "Handler attributes override route group metadata defaults")]
    public void MapGet_ShouldRespectHandlerMetadataAttributes()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var endpointMapBuilder = CreateEndpointMapBuilder(app.MapGroup("/users"));

        endpointMapBuilder.MapGet(endpointMapBuilder.Route, HandleWithMetadata);

        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("HandlerName");
        endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Handler summary");
    }

    [Fact(DisplayName = "MapGet supports method groups and endpoint filters")]
    public async Task MapGet_ShouldExecuteHandlerAndEndpointFilter()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var endpointMapBuilder = CreateEndpointMapBuilder(app.MapGroup("/users"));
        endpointMapBuilder.MapGet(endpointMapBuilder.Route, Handle)
            .AddEndpointFilter(async (context, next) => $"filtered:{await next(context)}");
        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        var context = new DefaultHttpContext { RequestServices = app.Services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        await endpoint.RequestDelegate!(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("filtered:handler");
    }

    [Fact(DisplayName = "CreateApplicationBuilder supports mapping a request pipeline")]
    public async Task CreateApplicationBuilder_ShouldSupportRequestPipelines()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var endpointMapBuilder = CreateEndpointMapBuilder(app.MapGroup("/users"));
        var pipeline = ((IEndpointRouteBuilder)endpointMapBuilder).CreateApplicationBuilder();
        pipeline.Run(static context => context.Response.WriteAsync("pipeline"));
        endpointMapBuilder.MapGet(endpointMapBuilder.Route, pipeline.Build());
        var endpoint = GetRouteEndpoint(app, "/users/{id:guid}");
        var context = new DefaultHttpContext { RequestServices = app.Services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        await endpoint.RequestDelegate!(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("pipeline");
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("UpdateUser");
    }

    [Fact(DisplayName = "ApplyMetadata supports routes mapped on the original group")]
    public void ApplyMetadata_ShouldSupportOriginalGroupRoutes()
    {
        AssertMappedEndpoint(
            static builder => builder.ApplyMetadata(builder.Group.MapGet(builder.Route, Handle)),
            "GET");
    }

    [Fact(DisplayName = "ApplyMetadata rejects a null route handler builder")]
    public void ApplyMetadata_ShouldValidateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/users");
        var endpointMapBuilder = CreateEndpointMapBuilder(group);

        var exception = Should.Throw<ArgumentNullException>(() => endpointMapBuilder.ApplyMetadata(null!));

        exception.ParamName.ShouldBe("builder");
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

    private static string Handle() => "handler";

    [EndpointName("HandlerName")]
    [EndpointSummary("Handler summary")]
    private static string HandleWithMetadata() => "handler";

    private static RouteEndpoint GetRouteEndpoint(
        WebApplication app,
        string routePattern)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == routePattern);
    }
}
