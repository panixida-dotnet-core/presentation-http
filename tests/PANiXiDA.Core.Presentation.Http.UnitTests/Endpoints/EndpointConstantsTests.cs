using PANiXiDA.Core.Presentation.Http.Endpoints;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;

public sealed class EndpointConstantsTests
{
    [Fact(DisplayName = "EndpointPrefix contains the versioned API prefix")]
    public void EndpointPrefix_ShouldContainVersionedApiPrefix()
    {
        EndpointConstants.EndpointPrefix.Should().Be("/api/v{version:apiVersion}");
    }
}
