using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Discovers and maps endpoints that belong to the specified endpoint group.
/// </summary>
public static class EndpointMapper
{
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
            endpoint.Map(group);
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
