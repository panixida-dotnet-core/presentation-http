using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class AntiforgeryConfiguration
{
    internal static IServiceCollection AddAntiforgeryConfiguration(this IServiceCollection services)
    {
        services.AddAntiforgery();

        return services;
    }

    internal static WebApplication UseAntiforgeryConfiguration(this WebApplication app)
    {
        app.UseAntiforgery();

        return app;
    }
}
