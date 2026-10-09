using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Application.Authentication.Abstractions;
using PANiXiDA.Core.Presentation.Http.Authentication;
using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Logging;
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
    /// Registers the default HTTP presentation services, including strict JSON contracts, API versioning, OpenAPI, validation, Problem Details, exception handling, health checks, and forwarded headers.
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
    /// Registers the default HTTP presentation services with strict JSON contracts and separate OpenAPI documents for each module and API version.
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
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddAuthenticationConfiguration(configuration);
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddApiVersioningConfiguration();
        services.AddJsonConfiguration();
        services.AddOpenApiConfiguration(configuration, moduleRegistry.Modules);
        services.AddProblemDetailsConfiguration();
        services.AddExceptionHandler<ClientAbortedExceptionHandler>();
        services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddValidation();
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Adds the HTTP presentation middleware and maps source-generated endpoint groups from the specified assemblies.
    /// </summary>
    /// <remarks>
    /// Configures forwarded headers, request logging, exception handling, HTTPS redirection, routing, then registered CORS, authentication and authorization.
    /// CORS policies, authentication schemes and authorization policies must be registered by the host. Do not add routing, CORS or authentication/authorization middleware separately.
    /// </remarks>
    /// <param name="app">The ASP.NET Core application instance.</param>
    /// <param name="assemblies">The assemblies containing generated endpoint registrations.</param>
    /// <returns>The original application instance for further configuration.</returns>
    public static WebApplication UseHttp(
        this WebApplication app,
        params Assembly[] assemblies)
    {
        app.UseForwardedHeadersConfiguration();
        app.UseMiddleware<LoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            using (app.Logger.BeginScope(HttpRequestLogScope.CreateEndpoint(context)))
            {
                await next(context);
            }
        });

        var serviceProviderIsService = app.Services.GetService<IServiceProviderIsService>();
        if (IsServiceRegistered<ICorsService>(app.Services, serviceProviderIsService))
        {
            var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>();
            app.UseCors(corsOptions.Value.DefaultPolicyName);
        }

        if (IsServiceRegistered<IAuthenticationSchemeProvider>(app.Services, serviceProviderIsService))
        {
            app.UseAuthentication();
        }

        app.Use(async (context, next) =>
        {
            using (app.Logger.BeginScope(HttpRequestLogScope.CreateUser(context)))
            {
                await next(context);
            }
        });

        if (IsServiceRegistered<IAuthorizationHandlerProvider>(app.Services, serviceProviderIsService))
        {
            app.UseAuthorization();
        }

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

    private static bool IsServiceRegistered<TService>(
        IServiceProvider services,
        IServiceProviderIsService? serviceProviderIsService)
        where TService : class
    {
        if (serviceProviderIsService is not null)
        {
            return serviceProviderIsService.IsService(typeof(TService));
        }

        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetService<TService>() is not null;
    }
}
