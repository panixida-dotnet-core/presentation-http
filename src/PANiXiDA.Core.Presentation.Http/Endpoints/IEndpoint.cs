using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Represents an endpoint that can be mapped to an ASP.NET Core route group.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Gets the endpoint route relative to the endpoint group route.
    /// </summary>
    string Route { get; }

    /// <summary>
    /// Gets the endpoint name used for route metadata and link generation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the endpoint summary used for OpenAPI metadata.
    /// </summary>
    string Summary { get; }

    /// <summary>
    /// Maps the endpoint to the specified endpoint map builder.
    /// </summary>
    /// <param name="builder">The endpoint map builder.</param>
    void Map(EndpointMapBuilder builder);
}

/// <summary>
/// Represents an endpoint that belongs to a specific endpoint group.
/// </summary>
/// <typeparam name="TGroup">The endpoint group type.</typeparam>
[SuppressMessage(
    "Major Code Smell",
    "S2326:Unused type parameters should be removed",
    Justification = "TGroup intentionally acts as a marker type for endpoint grouping.")]
public interface IEndpoint<TGroup> : IEndpoint
    where TGroup : IEndpointGroup
{
}
