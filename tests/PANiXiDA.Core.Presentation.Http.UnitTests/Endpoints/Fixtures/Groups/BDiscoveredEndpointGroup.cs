using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public sealed class BDiscoveredEndpointGroup : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMappingRecorder.Add(nameof(BDiscoveredEndpointGroup));
    }
}
