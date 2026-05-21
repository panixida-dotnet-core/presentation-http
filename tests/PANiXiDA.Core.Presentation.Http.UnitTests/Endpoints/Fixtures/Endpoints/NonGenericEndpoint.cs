using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class NonGenericEndpoint : IEndpoint
{
    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(NonGenericEndpoint));
    }
}
