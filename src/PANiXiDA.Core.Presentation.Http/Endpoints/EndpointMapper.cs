using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Configurations;
using PANiXiDA.Core.Presentation.Http.DependencyInjection;

using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Discovers and maps endpoints that belong to the specified endpoint group.
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

        var endpointGroup = ActivatorUtilities.CreateInstance<TGroup>(endpoints.ServiceProvider);
        var apiVersion = endpointGroup.ApiVersion;
        var apiVersionSet = endpoints.NewApiVersionSet(endpointGroup.Name)
            .HasApiVersion(apiVersion)
            .ReportApiVersions()
            .Build();

        var group = endpoints.MapGroup(EndpointConstants.EndpointPrefix)
            .MapGroup(endpointGroup.Route);

        group.WithTags(endpointGroup.Name);
        group.WithApiVersionSet(apiVersionSet);
        group.MapToApiVersion(apiVersion);

        var moduleRegistry = endpoints.ServiceProvider.GetService<HttpModuleRegistry>();

        if (moduleRegistry is not null &&
            moduleRegistry.TryGetModule(typeof(TGroup).Assembly, out var module))
        {
            group.WithMetadata(new HttpModuleMetadata(module.Name));
        }

        MapGroupEndpoints<TGroup>(group, endpoints.ServiceProvider);

        return group;
    }

    /// <summary>
    /// Finds endpoints for <typeparamref name="TGroup"/>, creates them through the service provider, and maps them to the specified route group.
    /// </summary>
    /// <typeparam name="TGroup">The endpoint group type.</typeparam>
    /// <param name="group">The route group to map endpoints to.</param>
    /// <param name="serviceProvider">The service provider used to create endpoint instances.</param>
    public static void MapGroupEndpoints<TGroup>(
        RouteGroupBuilder group,
        IServiceProvider serviceProvider)
        where TGroup : IEndpointGroup
    {
        var endpointTypes = GetEndpointTypes(typeof(TGroup).Assembly, typeof(TGroup));
        var endpoints = new List<IEndpoint>();

        foreach (var endpointType in endpointTypes)
        {
            var endpoint = (IEndpoint)ActivatorUtilities.CreateInstance(serviceProvider, endpointType);
            endpoints.Add(endpoint);
        }

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

    private static List<Type> GetEndpointTypes(Assembly assembly, Type groupType)
    {
        var result = new List<Type>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            var interfaces = type.GetInterfaces();

            foreach (var interfaceType in interfaces)
            {
                if (!interfaceType.IsGenericType)
                {
                    continue;
                }

                if (interfaceType.GetGenericTypeDefinition() != typeof(IEndpoint<>))
                {
                    continue;
                }

                var genericArguments = interfaceType.GetGenericArguments();

                if (genericArguments[0] != groupType)
                {
                    continue;
                }

                result.Add(type);
                break;
            }
        }

        result.Sort(static (left, right) =>
        {
            return StringComparer.Ordinal.Compare(left.FullName, right.FullName);
        });

        return result;
    }
}
