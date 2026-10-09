using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using System.Security.Claims;
using System.Collections;

namespace PANiXiDA.Core.Presentation.Http.Logging;

internal sealed class HttpRequestLogScope : IReadOnlyDictionary<string, object?>
{
    private const string UserIdKey = "enduser.id";
    private const string RouteKey = "http.route";
    private const string EndpointNameKey = "aspnetcore.endpoint.display_name";
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, object?> _attributes;
    private HttpContext? _httpContext;
    private string? _completedUserId;
    private Endpoint? _completedEndpoint;

    private HttpRequestLogScope(HttpContext httpContext, Dictionary<string, object?> attributes)
    {
        _httpContext = httpContext;
        _attributes = attributes;
    }

    public int Count => _attributes.Count;
    public IEnumerable<string> Keys => _attributes.Keys;
    public IEnumerable<object?> Values => Keys.Select(key => this[key]);
    public object? this[string key] => key switch
    {
        UserIdKey => GetUserId(),
        RouteKey => (GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText,
        EndpointNameKey => GetEndpoint()?.DisplayName,
        _ => _attributes[key]
    };

    internal static HttpRequestLogScope Create(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return new HttpRequestLogScope(httpContext, new Dictionary<string, object?>
        {
            ["network.protocol.name"] = "http",
            ["http.request.method"] = httpContext.Request.Method,
            ["url.path"] = httpContext.Request.Path.Value,
            ["url.query"] = httpContext.Request.QueryString.Value,
            [RouteKey] = null,
            [EndpointNameKey] = null,
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
            _completedEndpoint = GetEndpoint();
            _httpContext = null;
        }
    }

    public bool ContainsKey(string key) => _attributes.ContainsKey(key);

    public bool TryGetValue(string key, out object? value)
    {
        if (!_attributes.ContainsKey(key))
        {
            value = null;
            return false;
        }

        value = this[key];
        return true;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<string, object?>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Endpoint? GetEndpoint()
    {
        lock (_syncRoot)
        {
            return _httpContext is { } context
                ? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? context.GetEndpoint()
                : _completedEndpoint;
        }
    }

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
