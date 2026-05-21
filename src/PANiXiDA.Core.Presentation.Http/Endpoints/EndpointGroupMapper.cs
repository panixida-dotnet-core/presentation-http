using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

internal static class EndpointGroupMapper
{
    internal static void MapDiscoveredGroups(IEndpointRouteBuilder endpoints, Assembly assembly)
    {
        var groupTypes = GetGroupTypes(assembly);

        foreach (var groupType in groupTypes)
        {
            var group = (IEndpointGroup)ActivatorUtilities.CreateInstance(
                endpoints.ServiceProvider,
                groupType);

            group.Map(endpoints);
        }
    }

    private static List<Type> GetGroupTypes(Assembly assembly)
    {
        var result = new List<Type>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (!typeof(IEndpointGroup).IsAssignableFrom(type))
            {
                continue;
            }

            result.Add(type);
        }

        result.Sort(static (left, right) =>
        {
            return StringComparer.Ordinal.Compare(left.FullName, right.FullName);
        });

        return result;
    }
}
