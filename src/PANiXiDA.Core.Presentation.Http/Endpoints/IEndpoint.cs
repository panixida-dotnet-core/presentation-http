using Microsoft.AspNetCore.Routing;

using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Represents an endpoint that can be mapped to an ASP.NET Core route group.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps the endpoint to the specified route group.
    /// </summary>
    /// <param name="group">The route group to map the endpoint to.</param>
    void Map(RouteGroupBuilder group);
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
