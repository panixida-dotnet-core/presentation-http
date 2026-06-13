using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

public sealed class OtherEndpointGroup : IEndpointGroup
{
    public string Route { get; } = "/other";

    public string ResourceName { get; } = "Other";

    public ApiVersion ApiVersion { get; } = new(1, 0);

    public void Map(IEndpointRouteBuilder endpoints)
    {
    }
}
