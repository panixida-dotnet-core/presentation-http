using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public sealed class ADiscoveredEndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/a";

    public string Name { get; } = "A";

    public ApiVersion ApiVersion { get; } = new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
        EndpointMappingRecorder.Add(nameof(ADiscoveredEndpointGroup));
    }
}
