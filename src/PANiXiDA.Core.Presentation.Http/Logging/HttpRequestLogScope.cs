using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using System.Security.Claims;
using System.Collections;

namespace PANiXiDA.Core.Presentation.Http.Logging;

internal sealed class HttpRequestLogScope : IReadOnlyDictionary<string, object?>
{
    private const string UserIdKey = "enduser.id";
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, object?> _attributes;
    private HttpContext? _httpContext;
    private string? _completedUserId;

    private HttpRequestLogScope(HttpContext httpContext, Dictionary<string, object?> attributes)
    {
        _httpContext = httpContext;
        _attributes = attributes;
    }

    public int Count => _attributes.Count;
    public IEnumerable<string> Keys => _attributes.Keys;
    public IEnumerable<object?> Values => Keys.Select(key => this[key]);
    public object? this[string key] => key == UserIdKey ? GetUserId() : _attributes[key];

    internal static HttpRequestLogScope Create(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var endpoint = httpContext.GetEndpoint();
        var route = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : null;
        return new HttpRequestLogScope(httpContext, new Dictionary<string, object?>
        {
            ["network.protocol.name"] = "http",
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["url.query"] = httpContext.Request.QueryString.Value,
            ["http.route"] = route,
            ["aspnetcore.endpoint.display_name"] = endpoint?.DisplayName,
            [UserIdKey] = null,
            ["client.address"] = httpContext.Connection.RemoteIpAddress?.ToString(),
            ["user_agent.original"] = httpContext.Request.Headers.UserAgent.ToString(),
        });
    }

    internal void Complete()
    {
        lock (_syncRoot)
        {
            _completedUserId = GetUserId();
            _httpContext = null;
        }
    }

    public bool ContainsKey(string key) => _attributes.ContainsKey(key);

    public bool TryGetValue(string key, out object? value)
    {
        if (key == UserIdKey)
        {
            value = GetUserId();
            return true;
        }

        return _attributes.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<string, object?>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string? GetUserId()
    {
        lock (_syncRoot)
        {
            if (_httpContext is not { } context)
            {
                return _completedUserId;
            }

            var claims = context.User.Identities
                .Where(identity => identity.IsAuthenticated)
                .SelectMany(identity => identity.Claims);
            var claim = claims.FirstOrDefault(candidate => candidate.Type == "sub")
                ?? claims.FirstOrDefault(candidate => candidate.Type == ClaimTypes.NameIdentifier);

            return claim?.Value;
        }
    }
}
