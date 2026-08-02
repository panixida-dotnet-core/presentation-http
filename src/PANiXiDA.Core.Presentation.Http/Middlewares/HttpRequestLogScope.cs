using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using System.Security.Claims;

namespace PANiXiDA.Core.Presentation.Http.Middlewares;

internal static class HttpRequestLogScope
{
    internal static IReadOnlyDictionary<string, object?> Create(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var endpoint = httpContext.GetEndpoint();
        var route = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return new Dictionary<string, object?>
        {
            ["network.protocol.name"] = "http",
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["url.query"] = httpContext.Request.QueryString.Value,
            ["http.route"] = route,
            ["aspnetcore.endpoint.display_name"] = endpoint?.DisplayName,
            ["enduser.id"] = userId,
            ["client.address"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["user_agent.original"] = httpContext.Request.Headers.UserAgent.ToString(),
        };
    }
}
