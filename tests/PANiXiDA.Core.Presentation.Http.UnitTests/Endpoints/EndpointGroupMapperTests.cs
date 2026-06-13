using Microsoft.AspNetCore.Builder;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
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
        builder.Services.AddHttp(builder.Configuration);

        using var app = builder.Build();

        EndpointGroupMapper.MapDiscoveredGroups(app, typeof(ADiscoveredEndpointGroup).Assembly);

        EndpointMappingRecorder.Entries.Take(2).ShouldBe([
            nameof(ADiscoveredEndpointGroup),
            nameof(BDiscoveredEndpointGroup)
        ]);
        EndpointMappingRecorder.Entries.ShouldNotContain(nameof(AbstractDiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.ShouldNotContain(nameof(IDiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.ShouldContain(nameof(InternalDiscoveredEndpointGroup));
    }
}
