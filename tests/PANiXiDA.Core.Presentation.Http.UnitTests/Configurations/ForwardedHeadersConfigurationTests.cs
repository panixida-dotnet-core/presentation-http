using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact(DisplayName = "ForwardedHeaders configuration enables default forwarded headers")]
    public void AddForwardedHeadersConfiguration_ShouldUseDefaultHeaders()
    {
        var services = new ServiceCollection();
        var defaultOptions = new ForwardedHeadersOptions();

        var result = services.AddForwardedHeadersConfiguration(configuration: null);

        var options = CreateOptions(services);

        result.ShouldBeSameAs(services);
        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedHost |
            ForwardedHeaders.XForwardedProto);
        options.KnownIPNetworks.Count.ShouldBe(defaultOptions.KnownIPNetworks.Count);
        options.KnownProxies.Count.ShouldBe(defaultOptions.KnownProxies.Count);
    }

    [Fact(DisplayName = "ForwardedHeaders configuration binds standard options from configuration")]
    public void AddForwardedHeadersConfiguration_ShouldBindStandardOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [nameof(ForwardedHeadersOptions.ForwardedHeaders)] = nameof(ForwardedHeaders.XForwardedFor),
            [nameof(ForwardedHeadersOptions.ForwardLimit)] = "2",
            [nameof(ForwardedHeadersOptions.RequireHeaderSymmetry)] = "true",
            [nameof(ForwardedHeadersOptions.AllowedHosts) + ":0"] = "api.example.test"
        });

        services.AddForwardedHeadersConfiguration(configuration);

        var options = CreateOptions(services);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor);
        options.ForwardLimit.ShouldBe(2);
        options.RequireHeaderSymmetry.ShouldBeTrue();
        options.AllowedHosts.ShouldBe(["api.example.test"]);
    }

    [Fact(DisplayName = "ForwardedHeaders configuration accepts a custom section")]
    public void AddForwardedHeadersConfiguration_ShouldBindStandardOptionsFromCustomSection()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Http:ForwardedHeaders:ForwardedHeaders"] = nameof(ForwardedHeaders.XForwardedHost),
            ["Http:ForwardedHeaders:ForwardLimit"] = "5"
        });

        services.AddForwardedHeadersConfiguration(configuration.GetSection("Http:ForwardedHeaders"));

        var options = CreateOptions(services);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedHost);
        options.ForwardLimit.ShouldBe(5);
    }

    private static ForwardedHeadersOptions CreateOptions(IServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
