using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class HealthChecksConfiguration
{
    internal static IServiceCollection AddHealthChecksConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks();
        services.AddOptions<HealthCheckEndpointOptions>()
            .Bind(configuration.GetSection(nameof(HealthCheckEndpointOptions)))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Path) && options.Path.StartsWith('/'),
                "HealthCheckEndpointOptions.Path must be a non-empty path starting with '/'.")
            .ValidateOnStart();

        return services;
    }

    internal static WebApplication UseHealthChecksConfiguration(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<HealthCheckEndpointOptions>>().Value;
        app.MapHealthChecks(options.Path);

        return app;
    }
}
