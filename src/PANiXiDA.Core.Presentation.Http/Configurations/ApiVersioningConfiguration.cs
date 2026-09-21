using Asp.Versioning;

using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class ApiVersioningConfiguration
{
    internal const string TrimmingMessage = "Asp.Versioning.Mvc.ApiExplorer registers MVC services that do not support trimming or Native AOT.";

    [RequiresUnreferencedCode(TrimmingMessage)]
    internal static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
