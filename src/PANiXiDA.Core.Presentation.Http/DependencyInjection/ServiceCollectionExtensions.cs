using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PANiXiDA.Core.Application.Authentication.Abstractions;
using PANiXiDA.Core.Presentation.Http.Authentication;
using PANiXiDA.Core.Presentation.Http.Configurations;
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
    /// Registers the default HTTP presentation services, including authentication, authorization, antiforgery, CORS, strict JSON contracts, API versioning, OpenAPI, validation, Problem Details, exception handling, health checks, and forwarded headers.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration. The <c>ForwardedHeaders</c> section configures proxy headers; <c>OpenIddictValidationOptions</c> enables Bearer token introspection when present.</param>
    /// <returns>The original service collection for further configuration.</returns>
    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddHttpCore(services, configuration, []);
    }

    /// <summary>
    /// Registers the default HTTP presentation services, including authentication, authorization, antiforgery and CORS, with strict JSON contracts and separate OpenAPI documents for each module and API version.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">The application configuration. <c>HttpModules</c> defines module documents by assembly name; <c>OpenIddictValidationOptions</c> enables Bearer token introspection when present.</param>
    /// <param name="moduleAssemblies">The presentation assemblies to map and document per API version. Modules with only unversioned endpoints use a common document.</param>
    /// <returns>The original service collection for further configuration.</returns>
    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] moduleAssemblies)
    {
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
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddCorsConfiguration();
        services.AddAuthenticationConfiguration(configuration);
        services.AddAntiforgery();
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddApiVersioningConfiguration();
        services.AddJsonConfiguration();
        services.AddOpenApiConfiguration(configuration, moduleRegistry.Modules);
        services.AddProblemDetailsConfiguration();
        services.AddMiddlewareConfiguration();
        services.AddValidation();
        services.AddHealthChecksConfiguration(configuration);

        return services;
    }

    /// <summary>
    /// Adds the HTTP presentation middleware and maps source-generated endpoint groups from the specified assemblies.
    /// </summary>
    /// <remarks>
    /// Configures forwarded headers, request logging, exception handling, HTTPS redirection, routing, CORS, authentication, authorization and antiforgery.
    /// CORS policies, authentication schemes and authorization policies must be registered by the host. Do not add routing, CORS, authentication/authorization or antiforgery middleware separately.
    /// </remarks>
    /// <param name="app">The ASP.NET Core application instance.</param>
    /// <param name="assemblies">The assemblies containing generated endpoint registrations.</param>
    /// <returns>The original application instance for further configuration.</returns>
    public static WebApplication UseHttp(
        this WebApplication app,
        params Assembly[] assemblies)
    {
        app.UseForwardedHeadersConfiguration();
        app.UseMiddlewareConfiguration();
        app.UseCorsConfiguration();
        app.UseAuthenticationConfiguration();
        app.UseAntiforgery();
        app.UseOpenApiConfiguration();
        app.UseHealthChecksConfiguration();
        app.UseEndpointConfiguration(assemblies);

        return app;
    }
}
