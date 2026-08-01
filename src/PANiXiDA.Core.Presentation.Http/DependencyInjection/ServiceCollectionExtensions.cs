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
    /// <param name="configuration">The application configuration. The standard <c>ForwardedHeaders</c> section is used when present.</param>
    /// <returns>The original service collection for further configuration.</returns>
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddHttpCore(services, configuration, []);
    }

    /// <summary>
    /// Registers the default HTTP presentation services and separate OpenAPI documents for the specified modules.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration. The standard <c>ForwardedHeaders</c> section is used when present.</param>
    /// <param name="modules">The modules to map and expose as separate OpenAPI documents.</param>
    /// <returns>The original service collection for further configuration.</returns>
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration,
        params HttpModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        return AddHttpCore(services, configuration, modules);
    }

    private static IServiceCollection AddHttpCore(
        IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<HttpModule> modules)
    {
        var moduleRegistry = new HttpModuleRegistry(modules);

        services.AddSingleton(moduleRegistry);
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddApiVersioningConfiguration();
        services.AddOpenApiConfiguration(configuration, moduleRegistry.Modules);
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

        var mappedAssemblies = new HashSet<Assembly>();
        var moduleRegistry = app.Services.GetRequiredService<HttpModuleRegistry>();

        foreach (var module in moduleRegistry.Modules)
        {
            EndpointGroupMapper.MapDiscoveredGroups(app, module.PresentationAssembly);
            mappedAssemblies.Add(module.PresentationAssembly);
        }

        foreach (var assembly in assemblies)
        {
            if (!mappedAssemblies.Add(assembly))
            {
                continue;
            }

            EndpointGroupMapper.MapDiscoveredGroups(app, assembly);
        }

        return app;
    }
}
