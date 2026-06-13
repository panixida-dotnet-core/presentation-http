using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Provides route mapping helpers for a single endpoint route.
/// </summary>
public sealed class EndpointMapBuilder
{
    private readonly RouteGroupBuilder group;
    private readonly string route;
    private readonly string name;
    private readonly string summary;

    internal EndpointMapBuilder(
        RouteGroupBuilder group,
        string route,
        string name,
        string summary)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(summary);

        this.group = group;
        this.route = route;
        this.name = name;
        this.summary = summary;
    }

    /// <summary>
    /// Gets the route group to map the endpoint to.
    /// </summary>
    public RouteGroupBuilder Group
    {
        get
        {
            return group;
        }
    }

    /// <summary>
    /// Gets the endpoint route relative to the route group.
    /// </summary>
    public string Route
    {
        get
        {
            return route;
        }
    }

    /// <summary>
    /// Gets the endpoint name used for route metadata and link generation.
    /// </summary>
    public string Name
    {
        get
        {
            return name;
        }
    }

    /// <summary>
    /// Gets the endpoint summary used for OpenAPI metadata.
    /// </summary>
    public string Summary
    {
        get
        {
            return summary;
        }
    }

    /// <summary>
    /// Maps the endpoint route to an HTTP GET handler.
    /// </summary>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapGet(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapGet(route, handler));
    }

    /// <summary>
    /// Maps the endpoint route to an HTTP POST handler.
    /// </summary>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapPost(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapPost(route, handler));
    }

    /// <summary>
    /// Maps the endpoint route to an HTTP PUT handler.
    /// </summary>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapPut(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapPut(route, handler));
    }

    /// <summary>
    /// Maps the endpoint route to an HTTP PATCH handler.
    /// </summary>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapPatch(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapPatch(route, handler));
    }

    /// <summary>
    /// Maps the endpoint route to an HTTP DELETE handler.
    /// </summary>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapDelete(Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapDelete(route, handler));
    }

    /// <summary>
    /// Maps the endpoint route to the specified HTTP methods.
    /// </summary>
    /// <param name="httpMethods">The HTTP methods supported by the endpoint.</param>
    /// <param name="handler">The route handler.</param>
    /// <returns>The route handler builder for the mapped endpoint.</returns>
    public RouteHandlerBuilder MapMethods(IEnumerable<string> httpMethods, Delegate handler)
    {
        ArgumentNullException.ThrowIfNull(httpMethods);
        ArgumentNullException.ThrowIfNull(handler);

        return ApplyEndpointMetadata(group.MapMethods(route, httpMethods, handler));
    }

    private RouteHandlerBuilder ApplyEndpointMetadata(RouteHandlerBuilder builder)
    {
        return builder
            .WithName(name)
            .WithSummary(summary);
    }
}
