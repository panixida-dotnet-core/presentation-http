using Asp.Versioning;

using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class ApiVersioningConfiguration
{
    internal const string TrimmingMessage = "API Versioning's MVC explorer and OpenAPI integration do not support trimming or Native AOT.";
    internal const string GroupNameFormat = "'v'VVV";

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
            options.GroupNameFormat = GroupNameFormat;
            options.FormatGroupName = static (groupName, version) => $"{groupName}-{version}";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
