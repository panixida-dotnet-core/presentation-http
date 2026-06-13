using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Middlewares;

using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.DependencyInjection;

/// <summary>
/// Provides extension methods for registering and mapping application HTTP infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default HTTP presentation services, including API versioning, OpenAPI, Problem Details, exception handling, validation, health checks, and forwarded headers.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The forwarded headers configuration section, or <see langword="null"/> to use defaults.</param>
    /// <returns>The original service collection for further configuration.</returns>
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddApiVersioningConfiguration();
        services.AddOpenApiConfiguration();
        services.AddProblemDetailsConfiguration();
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddValidation();
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Adds the HTTP presentation middleware and maps endpoint groups from the specified assemblies.
    /// </summary>
    /// <param name="app">The ASP.NET Core application instance.</param>
    /// <param name="assemblies">The assemblies used to discover endpoint groups.</param>
    /// <returns>The original application instance for further configuration.</returns>
    public static WebApplication UseHttp(this WebApplication app, params Assembly[] assemblies)
    {
        app.UseForwardedHeadersConfiguration();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseMiddleware<LoggingMiddleware>();
        app.UseOpenApiConfiguration();
        app.MapHealthChecks("/health");

        foreach (var assembly in assemblies)
        {
            EndpointGroupMapper.MapDiscoveredGroups(app, assembly);
        }

        return app;
    }
}
