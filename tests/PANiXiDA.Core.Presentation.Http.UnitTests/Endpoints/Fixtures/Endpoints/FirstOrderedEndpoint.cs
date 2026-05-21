using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class FirstOrderedEndpoint : IEndpoint<OrderedEndpointGroup>
{
    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(FirstOrderedEndpoint));
        group.MapGet("/first", static () => "first");
    }
}
