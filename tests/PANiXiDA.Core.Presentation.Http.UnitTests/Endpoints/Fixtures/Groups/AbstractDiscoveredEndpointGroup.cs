using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public abstract class AbstractDiscoveredEndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/abstract";

    public string ResourceName { get; } = "Abstract";

    public ApiVersion ApiVersion { get; } = new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMappingRecorder.Add(nameof(AbstractDiscoveredEndpointGroup));
    }
}
