using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class FirstOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public string Route { get; } = "/first";

    public string Name { get; } = "FirstOrdered";

    public string Summary { get; } = "Gets the first ordered endpoint.";

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(FirstOrderedEndpoint));

        builder.MapGet(static () => "first");
    }
}
