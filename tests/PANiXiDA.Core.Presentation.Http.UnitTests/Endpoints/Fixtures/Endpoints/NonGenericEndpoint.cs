using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class NonGenericEndpoint : IEndpoint
{
    public string Route { get; } = "/non-generic";

    public string Name { get; } = "NonGeneric";

    public string Summary { get; } = "Gets the non-generic endpoint.";

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(NonGenericEndpoint));

        builder.MapGet(static () => "non-generic");
    }
}
