using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class EndpointWithComparableInterface :
    IEndpoint<OrderedEndpointGroup>,
    IComparable<EndpointWithComparableInterface>
{
    public int CompareTo(EndpointWithComparableInterface? other)
    {
        return 0;
    }

    public void Map(RouteGroupBuilder group)
    {
        EndpointMappingRecorder.Add(nameof(EndpointWithComparableInterface));
        group.MapGet("/comparable", static () => "comparable");
    }
}
