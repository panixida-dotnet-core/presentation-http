using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.Transformers;

internal sealed class BearerSecurityOpenApiOperationTransformer(
    IOptions<ScalarConfiguration> configuration) : IOpenApiOperationTransformer
{
    private const string SchemeName = "Bearer";

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var policyProvider = context.ApplicationServices.GetService<IAuthorizationPolicyProvider>();
        if (policyProvider is null)
        {
            return;
        }

        var policy = await AuthorizationPolicy.CombineAsync(
            policyProvider,
            metadata.OfType<IAuthorizeData>(),
            metadata.OfType<AuthorizationPolicy>());
        if (policy is null || !await AcceptsBearerAuthenticationAsync(policy, context.ApplicationServices))
        {
            return;
        }

        var document = context.Document!;
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.TryAdd(SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Access token without the Bearer prefix."
        });

        var schemeReference = new OpenApiSecuritySchemeReference(SchemeName, document);
        operation.Security ??= [];
        if (!operation.Security.Any(requirement => requirement.ContainsKey(schemeReference)))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [schemeReference] = []
            });
        }
    }

    private async Task<bool> AcceptsBearerAuthenticationAsync(
        AuthorizationPolicy policy,
        IServiceProvider services)
    {
        IEnumerable<string> schemes = policy.AuthenticationSchemes;
        if (policy.AuthenticationSchemes.Count == 0)
        {
            var schemeProvider = services.GetService<IAuthenticationSchemeProvider>();
            if (schemeProvider is null)
            {
                return false;
            }

            var defaultScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
            if (defaultScheme is null)
            {
                return false;
            }

            schemes = [defaultScheme.Name];
        }

        return schemes.Any(scheme =>
            scheme is SchemeName or BearerTokenDefaults.AuthenticationScheme ||
            configuration.Value.BearerAuthenticationSchemes.Contains(scheme, StringComparer.Ordinal));
    }
}
