using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class OtherGroupEndpoint : IEndpoint<OtherEndpointGroup>
{
    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(OtherGroupEndpoint));
    }
}
