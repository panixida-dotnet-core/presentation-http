using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class AuthenticationConfiguration
{
    internal static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
            .Validate(options => options.Issuer!.Scheme == Uri.UriSchemeHttps,
                "OpenIddictValidationOptions:Issuer must be an absolute HTTPS URI.")
            .Validate(options => options.Audiences.Count > 0 &&
                options.Audiences.All(audience => !string.IsNullOrWhiteSpace(audience)),
                "OpenIddictValidationOptions:Audiences must contain non-empty audience values.")
            .ValidateOnStart();

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddAuthorization();
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
}
