using Microsoft.AspNetCore.Routing;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Connects source-generated endpoint factories to HTTP route mapping.
/// This API is intended for generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EndpointRegistry
{
    private static readonly ConditionalWeakTable<Assembly, Registration> Registrations = new();

    /// <summary>
    /// Registers statically generated group mappings and endpoint factories for an assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the groups and endpoints.</param>
    /// <param name="mapGroups">The callback mapping all groups in deterministic type-name order.</param>
    /// <param name="createGroup">The callback creating a group using its generated constructor.</param>
    /// <param name="createEndpoints">The callback creating all endpoints belonging to a group.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The assembly already has a registration.</exception>
    public static void RegisterAssembly(
        Assembly assembly,
        Action<IEndpointRouteBuilder> mapGroups,
        Func<Type, IServiceProvider, IEndpointGroup> createGroup,
        Func<Type, IServiceProvider, IReadOnlyList<IEndpoint>> createEndpoints)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(mapGroups);
        ArgumentNullException.ThrowIfNull(createGroup);
        ArgumentNullException.ThrowIfNull(createEndpoints);

        Registrations.Add(assembly, new Registration(mapGroups, createGroup, createEndpoints));
    }

    internal static void MapGroups(IEndpointRouteBuilder endpoints, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        GetRegistration(assembly).MapGroupRoutes(endpoints);
    }

    internal static IEndpointGroup CreateGroup<TGroup>(IServiceProvider services)
        where TGroup : IEndpointGroup
    {
        return GetRegistration(typeof(TGroup).Assembly).GroupFactory(typeof(TGroup), services);
    }

    internal static IReadOnlyList<IEndpoint> CreateEndpoints<TGroup>(IServiceProvider services)
        where TGroup : IEndpointGroup
    {
        return GetRegistration(typeof(TGroup).Assembly).EndpointFactory(typeof(TGroup), services);
    }

    private static Registration GetRegistration(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

        if (Registrations.TryGetValue(assembly, out var registration))
        {
            return registration;
        }

        throw new InvalidOperationException(
            $"Generated endpoint registration was not found for assembly '{assembly.FullName}'. " +
            "Reference PANiXiDA.Core.Presentation.Http with its analyzers in the endpoint project and rebuild it.");
    }

    private sealed record Registration(
        Action<IEndpointRouteBuilder> MapGroupRoutes,
        Func<Type, IServiceProvider, IEndpointGroup> GroupFactory,
        Func<Type, IServiceProvider, IReadOnlyList<IEndpoint>> EndpointFactory);
}
