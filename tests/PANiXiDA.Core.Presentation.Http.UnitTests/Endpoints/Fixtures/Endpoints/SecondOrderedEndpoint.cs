using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class SecondOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public string Route { get; } = "/second";

    public string Name { get; } = "SecondOrdered";

    public string Summary { get; } = "Gets the second ordered endpoint.";

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(SecondOrderedEndpoint));

        builder.MapGet(static () => "second");
    }
}
