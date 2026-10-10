using Microsoft.AspNetCore.Http;

using OpenIddict.Abstractions;

using PANiXiDA.Core.Application.Authentication.Abstractions;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;

using ApplicationClaimTypes = PANiXiDA.Core.Application.Authentication.ClaimTypes;

namespace PANiXiDA.Core.Presentation.Http.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private IEnumerable<ClaimsIdentity> Identities => httpContextAccessor.HttpContext?.User.Identities
        .Where(identity => identity.IsAuthenticated) ?? [];

    private IEnumerable<Claim> Claims => Identities.SelectMany(identity => identity.Claims);

    public bool IsAuthenticated => Identities.Any();

    public Guid? UserId
    {
        get
        {
            var claim = Claims.FirstOrDefault(claim => claim.Type == OpenIddictConstants.Claims.Subject)
                ?? Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier);

            return Guid.TryParse(claim?.Value, out var userId) ? userId : null;
        }
    }

    public string? UserName => Identities
        .Select(identity => identity.Name ?? identity.FindFirst("name")?.Value)
        .FirstOrDefault(name => name is not null);

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            return [.. Identities
                .SelectMany(identity => identity.Claims
                    .Where(claim => claim.Type == identity.RoleClaimType || claim.Type == "role"))
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)];
        }
    }

    public bool TryGetClaimValue<T>(string claimType, [MaybeNullWhen(false)] out T value)
        where T : IParsable<T>
    {
        var claim = string.IsNullOrWhiteSpace(claimType)
            ? null
            : Claims.FirstOrDefault(claim => claim.Type == claimType);
        if (claim is not null && T.TryParse(claim.Value, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public bool HasPermission(string permission)
    {
        return !string.IsNullOrWhiteSpace(permission) && Claims.Any(claim =>
            claim.Type == ApplicationClaimTypes.Permission && claim.Value == permission);
    }
}
