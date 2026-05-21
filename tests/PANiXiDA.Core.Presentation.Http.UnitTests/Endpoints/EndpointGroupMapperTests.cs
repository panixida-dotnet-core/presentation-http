using Microsoft.AspNetCore.Builder;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointGroupMapperTests
{
    [Fact(DisplayName = "MapDiscoveredGroups maps discovered endpoint groups")]
    public void MapDiscoveredGroups_ShouldMapDiscoveredConcreteGroups()
    {
        EndpointMappingRecorder.Clear();

        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        EndpointGroupMapper.MapDiscoveredGroups(app, typeof(ADiscoveredEndpointGroup).Assembly);

        EndpointMappingRecorder.Entries.Should().ContainInOrder(
            nameof(ADiscoveredEndpointGroup),
            nameof(BDiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.Should().NotContain(nameof(AbstractDiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.Should().NotContain(nameof(IDiscoveredEndpointGroup));
    }
}
