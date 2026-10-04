using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace PANiXiDA.Core.Presentation.Http.Transformers;

internal sealed class BearerSecurityOpenApiOperationTransformer : IOpenApiOperationTransformer
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

        var requiresAuthorization = metadata.Any(item => item is IAuthorizeData or AuthorizationPolicy);
        if (!requiresAuthorization)
        {
            var policyProvider = context.ApplicationServices.GetService<IAuthorizationPolicyProvider>();
            requiresAuthorization = policyProvider is not null
                && await policyProvider.GetFallbackPolicyAsync() is not null;
        }

        if (!requiresAuthorization)
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
}
