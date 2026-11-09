using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OpenIddict.Abstractions;

using System.Security.Claims;
using System.Net;

namespace PANiXiDA.Core.Presentation.Http.Logging;

internal static class HttpRequestLogScope
{
    internal static IReadOnlyDictionary<string, object?> Create(HttpContext httpContext)
    {
        return new Dictionary<string, object?>
        {
            ["network.protocol.name"] = "http",
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["url.query"] = RedactAccessTokens(httpContext.Request.QueryString.Value),
            ["client.address"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["user_agent.original"] = httpContext.Request.Headers.UserAgent.ToString(),
        };
    }

    private static string? RedactAccessTokens(string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return query;
        }

        var parameters = query[1..].Split('&');
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var separator = parameter.IndexOf('=');
            var name = separator < 0 ? parameter : parameter[..separator];
            if (string.Equals(WebUtility.UrlDecode(name), OpenIddictConstants.Parameters.AccessToken, StringComparison.OrdinalIgnoreCase))
            {
                parameters[index] = name + "=[REDACTED]";
            }
        }

        return "?" + string.Join('&', parameters);
    }

    internal static IReadOnlyDictionary<string, object?> CreateEndpoint(HttpContext httpContext)
    {
        var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();

        return new Dictionary<string, object?>
        {
            ["http.route"] = (endpoint as RouteEndpoint)?.RoutePattern.RawText,
            ["aspnetcore.endpoint.display_name"] = endpoint?.DisplayName,
        };
    }

    internal static IReadOnlyDictionary<string, object?> CreateUser(HttpContext httpContext)
    {
        var claims = httpContext.User.Identities
            .Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.Claims);
        var userId = claims.FirstOrDefault(claim => claim.Type == OpenIddictConstants.Claims.Subject)
            ?? claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier);

        return new Dictionary<string, object?>
        {
            ["enduser.id"] = userId?.Value,
        };
    }
}
