using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

using PANiXiDA.Core.Presentation.Http.Authentication;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class AuthenticationConfiguration
{
    internal static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication();
        services.AddAuthorization();
        var policyEvaluator = services.Last(service => service.ServiceType == typeof(IPolicyEvaluator));
        if (policyEvaluator.ImplementationType == typeof(PolicyEvaluator))
        {
            services.Replace(ServiceDescriptor.Transient<IPolicyEvaluator, AccessTokenPolicyEvaluator>());
        }

        var section = configuration.GetSection(nameof(OpenIddictValidationOptions));
        if (!section.Exists())
        {
            return services;
        }

        services.AddOptions<OpenIddictValidationOptions>()
            .Configure(options =>
            {
                options.Issuer = section.GetValue<Uri>(nameof(options.Issuer));
                options.Audiences.UnionWith(section.GetSection(nameof(options.Audiences)).Get<string[]>() ?? []);
                options.ClientId = section[nameof(options.ClientId)];
                options.ClientSecret = section[nameof(options.ClientSecret)];
            })
            .ValidateOnStart();

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.UseIntrospection();
                options.UseSystemNetHttp();
                options.UseAspNetCore()
                    .DisableAccessTokenExtractionFromBodyForm()
                    .DisableAccessTokenExtractionFromQueryString();
            });

        return services;
    }

    internal static WebApplication UseAuthenticationConfiguration(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseUserScope();
        app.UseAuthorization();

        return app;
    }
}
