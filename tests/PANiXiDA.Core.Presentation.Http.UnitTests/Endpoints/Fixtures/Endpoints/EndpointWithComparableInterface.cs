using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;

public sealed class EndpointWithComparableInterface :
    IEndpoint<OrderedEndpointGroup>,
    IComparable<EndpointWithComparableInterface>
{
    public string Route { get; } = "/comparable";

    public string Name { get; } = "ComparableOrdered";

    public string Summary { get; } = "Gets the comparable ordered endpoint.";

    public int CompareTo(EndpointWithComparableInterface? other)
    {
        return 0;
    }

    public void Map(EndpointMapBuilder builder)
    {
        EndpointMappingRecorder.Add(nameof(EndpointWithComparableInterface));

        builder.MapGet(static () => "comparable");
    }
}
