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
    /// Applies the endpoint name and summary to a directly mapped ASP.NET Core route handler.
    /// Call standard Map methods at the handler declaration so the Request Delegate Generator can inspect it.
    /// </summary>
    /// <param name="builder">The mapped route handler builder.</param>
    /// <returns>The route handler builder with endpoint metadata.</returns>
    public RouteHandlerBuilder ApplyMetadata(RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithName(name)
            .WithSummary(summary);
    }
}
