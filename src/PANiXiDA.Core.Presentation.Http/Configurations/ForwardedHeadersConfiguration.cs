using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
                var settings = ForwardedHeadersSettings.FromOptions(options);
                section.Bind(settings);
                settings.Apply(options);
            });
        }

        return services;
    }

    internal static WebApplication UseForwardedHeadersConfiguration(this WebApplication app)
    {
        app.UseForwardedHeaders();

        return app;
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
