using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Modularity;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Maps source-generated endpoint registrations for the specified endpoint group.
/// </summary>
public static class EndpointMapper
{
    /// <summary>
    /// Creates a versioned route group from <typeparamref name="TGroup"/> metadata and maps its endpoints.
    /// </summary>
    /// <typeparam name="TGroup">The endpoint group type.</typeparam>
    /// <param name="endpoints">The application route builder.</param>
    /// <returns>The created route group.</returns>
    public static RouteGroupBuilder MapGroupEndpoints<TGroup>(IEndpointRouteBuilder endpoints)
        where TGroup : IEndpointGroup
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var endpointGroup = EndpointRegistry.CreateGroup<TGroup>(endpoints.ServiceProvider);
        var apiVersion = endpointGroup.ApiVersion;
        var apiVersionSet = endpoints
            .NewApiVersionSet(endpointGroup.Name)
            .HasApiVersion(apiVersion)
            .ReportApiVersions()
            .Build();

        var group = endpoints.MapGroup(EndpointConstants.EndpointPrefix)
            .MapGroup(endpointGroup.Route);

        group.WithTags(endpointGroup.Name);
        group.WithApiVersionSet(apiVersionSet);
        group.MapToApiVersion(apiVersion);

        MapGroupEndpoints<TGroup>(group, endpoints.ServiceProvider);

        return group;
    }

    /// <summary>
    /// Attaches HTTP module metadata and maps the generated endpoint factories for <typeparamref name="TGroup"/>.
    /// </summary>
    /// <typeparam name="TGroup">The endpoint group type.</typeparam>
    /// <param name="group">The route group to map endpoints to.</param>
    /// <param name="serviceProvider">The service provider used to create endpoint instances.</param>
    public static void MapGroupEndpoints<TGroup>(
        RouteGroupBuilder group,
        IServiceProvider serviceProvider)
        where TGroup : IEndpointGroup
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var moduleRegistry = serviceProvider.GetService<HttpModuleRegistry>();

        if (moduleRegistry is not null &&
            moduleRegistry.TryGetModule(typeof(TGroup).Assembly, out var module))
        {
            group.WithMetadata(module);
        }

        var endpoints = EndpointRegistry.CreateEndpoints<TGroup>(serviceProvider);

        foreach (var endpoint in endpoints)
        {
            var endpointMapBuilder = new EndpointMapBuilder(
                group,
                endpoint.Route,
                endpoint.Name,
                endpoint.Summary);
            endpoint.Map(endpointMapBuilder);
        }
    }
}
