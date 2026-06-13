using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointMapperTests
{
    [Fact(DisplayName = "MapGroupEndpoints maps selected group endpoints by type name")]
    public void MapGroupEndpoints_ShouldMapSelectedGroupEndpointsByTypeName()
    {
        EndpointMappingRecorder.Clear();

        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();

        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/test");

        EndpointMapper.MapGroupEndpoints<OrderedEndpointGroup>(group, serviceProvider);

        EndpointMappingRecorder.Entries.ShouldBe([
            nameof(EndpointWithComparableInterface),
            nameof(FirstOrderedEndpoint),
            nameof(SecondOrderedEndpoint)
        ]);
    }

    [Fact(DisplayName = "MapGroupEndpoints maps a versioned group from endpoint group metadata")]
    public void MapGroupEndpoints_ShouldMapVersionedGroupFromEndpointGroupMetadata()
    {
        EndpointMappingRecorder.Clear();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttp(builder.Configuration);

        using var app = builder.Build();

        var result = EndpointMapper.MapGroupEndpoints<OrderedEndpointGroup>(app);

        result.ShouldNotBeNull();
        EndpointMappingRecorder.Entries.ShouldBe([
            nameof(EndpointWithComparableInterface),
            nameof(FirstOrderedEndpoint),
            nameof(SecondOrderedEndpoint)
        ]);
        var routePatterns = GetRoutePatterns(app);

        routePatterns.ShouldContain("/api/v{version:apiVersion}/ordered/comparable");
        routePatterns.ShouldContain("/api/v{version:apiVersion}/ordered/first");
        routePatterns.ShouldContain("/api/v{version:apiVersion}/ordered/second");

        var firstEndpoint = GetRouteEndpoint(app, "/api/v{version:apiVersion}/ordered/first");
        firstEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName.ShouldBe("FirstOrdered");
        firstEndpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary.ShouldBe("Gets the first ordered endpoint.");
    }

    private static List<string?> GetRoutePatterns(WebApplication app)
    {
        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)];
    }

    private static RouteEndpoint GetRouteEndpoint(WebApplication app, string routePattern)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == routePattern);
    }
}
