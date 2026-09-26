using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Net;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class ForwardedHeadersConfiguration
{
    private const string SectionName = "ForwardedHeaders";

    internal static IServiceCollection AddForwardedHeadersConfiguration(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        if (configuration is not null)
        {
            var section = ResolveConfiguration(configuration);
            services.AddSingleton<IOptionsChangeTokenSource<ForwardedHeadersOptions>>(
                new ConfigurationChangeTokenSource<ForwardedHeadersOptions>(section));
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                BindOptions(options, section);
            });
        }

        return services;
    }

    internal static WebApplication UseForwardedHeadersConfiguration(this WebApplication app)
    {
        app.UseForwardedHeaders();

        return app;
    }

    private static void BindOptions(
        ForwardedHeadersOptions options,
        IConfiguration configuration)
    {
        options.ForwardedForHeaderName = configuration.GetValue(
            nameof(options.ForwardedForHeaderName),
            options.ForwardedForHeaderName);
        options.ForwardedHostHeaderName = configuration.GetValue(
            nameof(options.ForwardedHostHeaderName),
            options.ForwardedHostHeaderName);
        options.ForwardedProtoHeaderName = configuration.GetValue(
            nameof(options.ForwardedProtoHeaderName),
            options.ForwardedProtoHeaderName);
        options.ForwardedPrefixHeaderName = configuration.GetValue(
            nameof(options.ForwardedPrefixHeaderName),
            options.ForwardedPrefixHeaderName);
        options.OriginalForHeaderName = configuration.GetValue(
            nameof(options.OriginalForHeaderName),
            options.OriginalForHeaderName);
        options.OriginalHostHeaderName = configuration.GetValue(
            nameof(options.OriginalHostHeaderName),
            options.OriginalHostHeaderName);
        options.OriginalProtoHeaderName = configuration.GetValue(
            nameof(options.OriginalProtoHeaderName),
            options.OriginalProtoHeaderName);
        options.OriginalPrefixHeaderName = configuration.GetValue(
            nameof(options.OriginalPrefixHeaderName),
            options.OriginalPrefixHeaderName);
        options.ForwardedHeaders = configuration.GetValue(
            nameof(options.ForwardedHeaders),
            options.ForwardedHeaders);
        options.RequireHeaderSymmetry = configuration.GetValue(
            nameof(options.RequireHeaderSymmetry),
            options.RequireHeaderSymmetry);

        if (configuration.GetChildren().Any(section =>
            StringComparer.OrdinalIgnoreCase.Equals(
                section.Key,
                nameof(options.ForwardLimit))))
        {
            options.ForwardLimit = configuration.GetValue<int?>(nameof(options.ForwardLimit));
        }

        var hosts = configuration
            .GetSection(nameof(options.AllowedHosts))
            .Get<string[]>() ?? [];
        foreach (var host in hosts)
        {
            options.AllowedHosts.Add(host);
        }

        var proxies = configuration
            .GetSection(nameof(options.KnownProxies))
            .Get<string[]>() ?? [];
        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        var networks = configuration
            .GetSection(nameof(options.KnownIPNetworks))
            .GetChildren()
            .Concat(configuration.GetSection("KnownNetworks").GetChildren());
        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(
                IPAddress.Parse(network.GetValue("Prefix", string.Empty)),
                network.GetValue<int>("PrefixLength")));
        }
    }

    private static IConfiguration ResolveConfiguration(IConfiguration configuration)
    {
        IConfigurationSection forwardedHeadersSection = configuration.GetSection(SectionName);

        if (forwardedHeadersSection.GetChildren().Any())
        {
            return forwardedHeadersSection;
        }

        return configuration;
    }
}
