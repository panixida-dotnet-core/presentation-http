using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

using OpenIddict.Abstractions;

namespace PANiXiDA.Core.Presentation.Http.Authentication;

internal sealed class AccessTokenPolicyEvaluator(IAuthorizationService authorizationService)
    : PolicyEvaluator(authorizationService)
{
    public override async Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var result = await base.AuthenticateAsync(policy, context);
        if (policy.AuthenticationSchemes.Count == 0
            || result is not { Succeeded: true, Properties: { } properties })
        {
            return result;
        }

        foreach (var scheme in policy.AuthenticationSchemes.Reverse())
        {
            var original = await context.AuthenticateAsync(scheme);
            if (!original.Succeeded || !original.Principal.Identities.Any(identity => identity.IsAuthenticated))
            {
                continue;
            }

            var token = original.Properties.GetTokenValue(OpenIddictConstants.Parameters.AccessToken);
            if (token is not null)
            {
                properties.StoreTokens([new AuthenticationToken
                {
                    Name = OpenIddictConstants.Parameters.AccessToken,
                    Value = token
                }]);
            }

            break;
        }

        return result;
    }
}
