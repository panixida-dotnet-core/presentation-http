using Microsoft.AspNetCore.Builder;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class FirstOrderedV2Endpoint : IEndpoint<OrderedV2EndpointGroup>
{
    public string Route { get; } = "/first";

    public string Name { get; } = "FirstOrderedV2";

    public string Summary { get; } = "Gets the first ordered endpoint for version two.";

    public void Map(EndpointMapBuilder builder)
    {
        builder.MapGet(builder.Route, static () => "first-v2");
    }
}
