using Microsoft.AspNetCore.Http;

using PANiXiDA.Core.Application.Authentication.Abstractions;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;

using ApplicationClaimTypes = PANiXiDA.Core.Application.Authentication.ClaimTypes;

namespace PANiXiDA.Core.Presentation.Http.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsIdentity? Identity => httpContextAccessor.HttpContext?.User.Identities
        .FirstOrDefault(identity => identity.IsAuthenticated);

    public bool IsAuthenticated => Identity is not null;

    public Guid? UserId
    {
        get
        {
            var identity = Identity;
            var claim = identity?.FindFirst("sub") ?? identity?.FindFirst(ClaimTypes.NameIdentifier);

            return Guid.TryParse(claim?.Value, out var userId) ? userId : null;
        }
    }

    public string? UserName => Identity?.Name ?? Identity?.FindFirst("name")?.Value;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var identity = Identity;
            if (identity is null)
            {
                return [];
            }

            return [.. identity.Claims
                .Where(claim => claim.Type == identity.RoleClaimType || claim.Type == "role")
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)];
        }
    }

    public bool TryGetClaimValue<T>(string claimType, [MaybeNullWhen(false)] out T value)
        where T : IParsable<T>
    {
        ArgumentNullException.ThrowIfNull(claimType);

        var claim = Identity?.FindFirst(claimType);
        if (claim is not null && T.TryParse(claim.Value, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public bool HasPermission(string permission)
    {
        return !string.IsNullOrWhiteSpace(permission) && Identity?.Claims.Any(claim =>
            claim.Type == ApplicationClaimTypes.Permission && claim.Value == permission) == true;
    }
}
