using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public abstract class AbstractOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(AbstractOrderedEndpoint));
    }
}
