using Microsoft.AspNetCore.Routing;

namespace PANiXiDA.Core.Presentation.Http.Endpoints;

/// <summary>
/// Represents an endpoint group that can be mapped to ASP.NET Core routes.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>
    /// Maps the endpoint group to the specified route builder.
    /// </summary>
    /// <param name="endpoints">The application route builder.</param>
    void Map(IEndpointRouteBuilder endpoints);
}
