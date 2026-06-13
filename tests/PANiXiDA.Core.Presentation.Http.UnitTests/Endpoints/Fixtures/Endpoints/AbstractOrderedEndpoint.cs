using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public abstract class AbstractOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public string Route { get; } = "/abstract";

    public string Name { get; } = "AbstractOrdered";

    public string Summary { get; } = "Gets the abstract ordered endpoint.";

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(AbstractOrderedEndpoint));

        builder.MapGet(static () => "abstract");
    }
}
