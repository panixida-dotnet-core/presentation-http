using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class OtherGroupEndpoint : IEndpoint<OtherEndpointGroup>
{
    public string Route { get; } = "/other";

    public string Name { get; } = "OtherGroup";

    public string Summary { get; } = "Gets the other group endpoint.";

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(OtherGroupEndpoint));

        builder.MapGet(static () => "other");
    }
}
