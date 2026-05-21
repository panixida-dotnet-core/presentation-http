using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class ForwardedHeadersConfiguration
{
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
        });

        if (configuration is not null)
        {
            services.Configure<ForwardedHeadersOptions>(configuration);
        }

        return services;
    }

    internal static WebApplication UseForwardedHeadersConfiguration(this WebApplication app)
    {
        app.UseForwardedHeaders();

        return app;
    }
}
