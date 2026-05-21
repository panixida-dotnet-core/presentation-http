using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class SecondOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(SecondOrderedEndpoint));
        group.MapGet("/second", static () => "second");
    }
}
