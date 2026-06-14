using Asp.Versioning;

using Microsoft.AspNetCore.Routing;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Represents an endpoint group that can be mapped to ASP.NET Core routes.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>
    /// Gets the endpoint group route relative to the versioned API route prefix.
    /// </summary>
    string Route { get; }

    /// <summary>
    /// Gets the API resource name used for tags and version set metadata.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the endpoint group API version.
    /// </summary>
    ApiVersion ApiVersion { get; }

    /// <summary>
    /// Maps the endpoint group to the specified route builder.
    /// </summary>
    /// <param name="endpoints">The application route builder.</param>
    void Map(IEndpointRouteBuilder endpoints);
}
