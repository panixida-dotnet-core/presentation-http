using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Configurations;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Configurations;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact(DisplayName = "ForwardedHeaders configuration enables default forwarded headers")]
    public void AddForwardedHeadersConfiguration_ShouldUseDefaultHeaders()
    {
        var services = new ServiceCollection();

        var result = services.AddForwardedHeadersConfiguration(configuration: null);

        var options = CreateOptions(services);

        result.ShouldBeSameAs(services);
        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedHost |
            ForwardedHeaders.XForwardedProto);
        options.KnownIPNetworks.ShouldBeEmpty();
        options.KnownProxies.ShouldBeEmpty();
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

    [Fact(DisplayName = "ForwardedHeaders configuration binds standard section from root configuration")]
    public void AddForwardedHeadersConfiguration_ShouldBindStandardSectionFromRootConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:ForwardedHeaders"] = nameof(ForwardedHeaders.XForwardedProto),
            ["ForwardedHeaders:ForwardLimit"] = "3",
            ["ForwardedHeaders:AllowedHosts:0"] = "api.example.test"
        });

        services.AddForwardedHeadersConfiguration(configuration);

        var options = CreateOptions(services);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.ShouldBe(3);
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

    [Fact(DisplayName = "Generated forwarded headers binding preserves header names, proxy addresses and network lists")]
    public void AddForwardedHeadersConfiguration_ShouldBindHeadersAndTrustedNetworks()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:ForwardedForHeaderName"] = "Custom-For",
            ["ForwardedHeaders:ForwardedHostHeaderName"] = "Custom-Host",
            ["ForwardedHeaders:ForwardedProtoHeaderName"] = "Custom-Proto",
            ["ForwardedHeaders:ForwardedPrefixHeaderName"] = "Custom-Prefix",
            ["ForwardedHeaders:OriginalForHeaderName"] = "Original-For",
            ["ForwardedHeaders:OriginalHostHeaderName"] = "Original-Host",
            ["ForwardedHeaders:OriginalProtoHeaderName"] = "Original-Proto",
            ["ForwardedHeaders:OriginalPrefixHeaderName"] = "Original-Prefix",
            ["ForwardedHeaders:KnownProxies:0"] = "192.0.2.1",
            ["ForwardedHeaders:KnownIPNetworks:0:Prefix"] = "10.0.0.0",
            ["ForwardedHeaders:KnownIPNetworks:0:PrefixLength"] = "8",
            ["ForwardedHeaders:KnownNetworks:0:Prefix"] = "192.168.0.0",
            ["ForwardedHeaders:KnownNetworks:0:PrefixLength"] = "16",
            ["ForwardedHeaders:ForwardLimit"] = null
        });

        services.AddForwardedHeadersConfiguration(configuration);
        var options = CreateOptions(services);

        options.ForwardedForHeaderName.ShouldBe("Custom-For");
        options.ForwardedHostHeaderName.ShouldBe("Custom-Host");
        options.ForwardedProtoHeaderName.ShouldBe("Custom-Proto");
        options.ForwardedPrefixHeaderName.ShouldBe("Custom-Prefix");
        options.OriginalForHeaderName.ShouldBe("Original-For");
        options.OriginalHostHeaderName.ShouldBe("Original-Host");
        options.OriginalProtoHeaderName.ShouldBe("Original-Proto");
        options.OriginalPrefixHeaderName.ShouldBe("Original-Prefix");
        options.KnownProxies.ShouldBe([IPAddress.Parse("192.0.2.1")]);
        options.KnownIPNetworks.ShouldBe([System.Net.IPNetwork.Parse("10.0.0.0/8"), System.Net.IPNetwork.Parse("192.168.0.0/16")]);
        options.ForwardLimit.ShouldBeNull();
    }

    [Fact(DisplayName = "Generated forwarded headers binding preserves option configuration and reload notifications")]
    public void AddForwardedHeadersConfiguration_ShouldPreserveDefaultsAndReload()
    {
        var services = new ServiceCollection();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.OriginalHostHeaderName = "Previous-Host";
            options.AllowedHosts.Add("previous.example");
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:ForwardLimit"] = "2",
            ["ForwardedHeaders:AllowedHosts:0"] = "current.example"
        }).Build();
        services.AddForwardedHeadersConfiguration(configuration);
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<ForwardedHeadersOptions>>();
        monitor.CurrentValue.ForwardLimit.ShouldBe(2);

        configuration["ForwardedHeaders:ForwardLimit"] = "4";
        configuration.Reload();

        monitor.CurrentValue.ForwardLimit.ShouldBe(4);
        monitor.CurrentValue.OriginalHostHeaderName.ShouldBe("Previous-Host");
        monitor.CurrentValue.AllowedHosts.ShouldBe(["previous.example", "current.example"]);
    }

    [Theory(DisplayName = "Generated forwarded headers binding rejects invalid proxy and network values")]
    [InlineData("KnownProxies:0", "invalid-address")]
    [InlineData("KnownIPNetworks:0:Prefix", "invalid-address")]
    public void AddForwardedHeadersConfiguration_ShouldRejectInvalidTrustConfiguration(
        string key,
        string value)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?> { ["ForwardedHeaders:" + key] = value });
        services.AddForwardedHeadersConfiguration(configuration);

        Should.Throw<FormatException>(() => CreateOptions(services));
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
