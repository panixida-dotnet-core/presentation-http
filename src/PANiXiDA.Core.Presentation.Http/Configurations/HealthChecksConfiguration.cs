using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Options.HealthCheck;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class HealthChecksConfiguration
{
    internal static IServiceCollection AddHealthChecksConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks();
        services.AddSingleton<IValidateOptions<HealthCheckOptions>, HealthCheckOptionsValidator>();
        services.AddOptions<HealthCheckOptions>()
            .Bind(configuration.GetSection(HealthCheckOptions.SectionName))
            .ValidateOnStart();

        return services;
    }

    internal static WebApplication UseHealthChecksConfiguration(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<HealthCheckOptions>>().Value;
        app.MapHealthChecks(options.Path);

        return app;
    }
}
