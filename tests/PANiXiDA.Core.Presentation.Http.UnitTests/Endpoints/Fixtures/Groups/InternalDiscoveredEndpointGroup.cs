using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

internal sealed class InternalDiscoveredEndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/internal";

    public string ResourceName { get; } = "Internal";

    public ApiVersion ApiVersion { get; } = new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMappingRecorder.Add(nameof(InternalDiscoveredEndpointGroup));
    }
}
