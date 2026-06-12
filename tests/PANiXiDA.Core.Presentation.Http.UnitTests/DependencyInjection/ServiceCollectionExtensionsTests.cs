using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints;
using PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddHttp registers HTTP infrastructure and returns the same service collection")]
    public void AddHttp_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddHttp(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact(DisplayName = "AddHttp applies ForwardedHeaders configuration")]
    public void AddHttp_ShouldApplyForwardedHeadersConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(ForwardedHeadersOptions.ForwardLimit)] = "4"
            })
            .Build();

        services.AddHttp(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.Should().Be(4);
    }

    [Fact(DisplayName = "AddHttp uses default settings when configuration is null")]
    public void AddHttp_ShouldUseDefaultForwardedHeadersConfigurationIfConfigurationIsNull()
    {
        var services = new ServiceCollection();
        IConfiguration? configuration = null;

        var result = services.AddHttp(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact(DisplayName = "UseHttp adds middleware and maps groups from the provided assemblies")]
    public void UseHttp_ShouldReturnSameApplicationAndMapEndpointGroups()
    {
        EndpointMappingRecorder.Clear();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.Services.AddHttp(builder.Configuration);

        using var app = builder.Build();

        var result = app.UseHttp(typeof(ADiscoveredEndpointGroup).Assembly);

        result.Should().BeSameAs(app);
        EndpointMappingRecorder.Entries.Should().Contain(nameof(ADiscoveredEndpointGroup));
        EndpointMappingRecorder.Entries.Should().Contain(nameof(BDiscoveredEndpointGroup));
    }
}
