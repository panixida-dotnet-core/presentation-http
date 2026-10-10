using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Middlewares;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class MiddlewareConfiguration
{
    internal static IServiceCollection AddMiddlewareConfiguration(this IServiceCollection services)
    {
        services.AddExceptionHandler<ClientAbortedExceptionHandler>();
        services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        services.AddExceptionHandler<ExceptionHandler>();

        return services;
    }

    internal static WebApplication UseMiddlewareConfiguration(this WebApplication app)
    {
        app.UseMiddleware<LoggingMiddleware>();
        app.UseExceptionHandler();

        return app;
    }
}
