using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Logging;
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

    internal static WebApplication UseEndpointScope(this WebApplication app)
    {
        return UseScope(app, HttpRequestLogScope.CreateEndpoint);
    }

    internal static WebApplication UseUserScope(this WebApplication app)
    {
        return UseScope(app, HttpRequestLogScope.CreateUser);
    }

    private static WebApplication UseScope(
        WebApplication app,
        Func<HttpContext, IReadOnlyDictionary<string, object?>> createScope)
    {
        app.Use(async (context, next) =>
        {
            using (app.Logger.BeginScope(createScope(context)))
            {
                await next(context);
            }
        });

        return app;
    }
}
