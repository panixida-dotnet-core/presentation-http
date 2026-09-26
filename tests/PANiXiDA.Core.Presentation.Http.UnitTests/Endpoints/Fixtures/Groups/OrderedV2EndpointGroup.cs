using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public sealed class OrderedV2EndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/ordered";

    public string Name { get; } = "Ordered";

    public ApiVersion ApiVersion { get; } = new(2, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMapper.MapGroupEndpoints<OrderedV2EndpointGroup>(endpoints);
    }
}
