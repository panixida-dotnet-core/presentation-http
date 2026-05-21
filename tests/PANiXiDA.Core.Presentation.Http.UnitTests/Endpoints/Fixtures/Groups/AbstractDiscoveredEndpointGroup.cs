using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public abstract class AbstractDiscoveredEndpointGroup : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMappingRecorder.Add(nameof(AbstractDiscoveredEndpointGroup));
    }
}
