using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class ApiVersioningConfigurationTests
{
    [Fact(DisplayName = "API versioning configuration registers expected options")]
    public void AddApiVersioningConfiguration_ShouldRegisterExpectedOptions()
    {
        var services = new ServiceCollection();

        var result = services.AddApiVersioningConfiguration();

        result.ShouldBeSameAs(services);

        using var serviceProvider = services.BuildServiceProvider();

        var apiVersioningOptions = serviceProvider
            .GetRequiredService<IOptions<ApiVersioningOptions>>()
            .Value;

        apiVersioningOptions.DefaultApiVersion.ShouldBe(new ApiVersion(1, 0));
        apiVersioningOptions.AssumeDefaultVersionWhenUnspecified.ShouldBeFalse();
        apiVersioningOptions.ReportApiVersions.ShouldBeTrue();
        apiVersioningOptions.ApiVersionReader.ShouldBeOfType<UrlSegmentApiVersionReader>();

        var apiExplorerOptions = serviceProvider
            .GetRequiredService<IOptions<ApiExplorerOptions>>()
            .Value;

        apiExplorerOptions.GroupNameFormat.ShouldBe("'v'V");
        apiExplorerOptions.SubstituteApiVersionInUrl.ShouldBeTrue();
    }
}
