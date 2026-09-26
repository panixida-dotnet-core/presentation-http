using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Middlewares;
using PANiXiDA.Core.Presentation.Http.Modularity;

using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.DependencyInjection;

/// <summary>
/// Provides extension methods for registering and mapping application HTTP infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default HTTP presentation services, including API versioning, OpenAPI, validation, Problem Details, exception handling, health checks, and forwarded headers.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration. The standard <c>ForwardedHeaders</c> section is used when present.</param>
    /// <returns>The original service collection for further configuration.</returns>
    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddHttpCore(services, configuration, []);
    }

    /// <summary>
    /// Registers the default HTTP presentation services and separate OpenAPI documents for each module and API version.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration. Module document names and titles are read from the <c>HttpModules</c> section by presentation assembly name.</param>
    /// <param name="moduleAssemblies">The presentation assemblies to map and document per API version. Modules with only unversioned endpoints use a common document.</param>
    /// <returns>The original service collection for further configuration.</returns>
    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] moduleAssemblies)
    {
        ArgumentNullException.ThrowIfNull(moduleAssemblies);

        return AddHttpCore(services, configuration, moduleAssemblies);
    }

    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    private static IServiceCollection AddHttpCore(
        IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<Assembly> moduleAssemblies)
    {
        var moduleRegistry = new HttpModuleRegistry(configuration, moduleAssemblies);

        services.AddSingleton(moduleRegistry);
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddApiVersioningConfiguration();
        services.AddJsonConfiguration();
        services.AddOpenApiConfiguration(configuration, moduleRegistry.Modules);
        services.AddProblemDetailsConfiguration();
        services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddValidation();
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Adds the HTTP presentation middleware and maps source-generated endpoint groups from the specified assemblies.
    /// </summary>
    /// <param name="app">The ASP.NET Core application instance.</param>
    /// <param name="assemblies">The assemblies containing generated endpoint registrations.</param>
    /// <returns>The original application instance for further configuration.</returns>
    public static WebApplication UseHttp(
        this WebApplication app,
        params Assembly[] assemblies)
    {
        app.UseForwardedHeadersConfiguration();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseMiddleware<LoggingMiddleware>();
        app.UseOpenApiConfiguration();
        app.MapHealthChecks("/health");

        var mappedAssemblies = new HashSet<Assembly>();
        var moduleRegistry = app.Services.GetRequiredService<HttpModuleRegistry>();
        var moduleAssemblies = moduleRegistry.Modules.Select(
            static module => module.PresentationAssembly);

        foreach (var presentationAssembly in moduleAssemblies)
        {
            EndpointRegistry.MapGroups(app, presentationAssembly);
            mappedAssemblies.Add(presentationAssembly);
        }

        foreach (var assembly in assemblies)
        {
            if (!mappedAssemblies.Add(assembly))
            {
                continue;
            }

            EndpointRegistry.MapGroups(app, assembly);
        }

        return app;
    }
}
