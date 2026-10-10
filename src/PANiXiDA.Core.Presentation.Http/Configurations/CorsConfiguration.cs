using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class CorsConfiguration
{
    internal static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors();

        return services;
    }

    internal static WebApplication UseCorsConfiguration(this WebApplication app)
    {
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>();
        app.UseCors(corsOptions.Value.DefaultPolicyName);

        return app;
    }
}
