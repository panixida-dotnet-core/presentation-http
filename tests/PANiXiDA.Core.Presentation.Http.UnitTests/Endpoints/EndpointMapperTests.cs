using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointMapperTests
{
    [Fact(DisplayName = "MapGroupEndpoints maps selected group endpoints by type name")]
    public void MapGroupEndpoints_ShouldMapSelectedGroupEndpointsByTypeName()
    {
        EndpointMappingRecorder.Clear();

        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();

        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var group = app.MapGroup("/test");

        EndpointMapper.MapGroupEndpoints<OrderedEndpointGroup>(group, serviceProvider);

        EndpointMappingRecorder.Entries.Should().Equal(
            nameof(EndpointWithComparableInterface),
            nameof(FirstOrderedEndpoint),
            nameof(SecondOrderedEndpoint));
    }
}
