using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Modularity;

using Scalar.AspNetCore;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class OpenApiConfiguration
{
    internal static IServiceCollection AddOpenApiConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<HttpModule> modules)
    {
        if (modules.Count == 0)
        {
            services.AddOpenApi(options =>
            {
                options.AddScalarTransformers();
            });
        }
        else
        {
            foreach (var moduleName in modules.Select(static module => module.Name))
            {
                services.AddOpenApi(moduleName, options =>
                {
                    options.AddScalarTransformers();
                    options.ShouldInclude = description =>
                    {
                        return ShouldInclude(description, moduleName);
                    };
                });
            }
        }

        services.Configure<ScalarConfiguration>(
            configuration.GetSection(nameof(ScalarConfiguration)));

        return services;
    }

    internal static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            var scalarConfiguration = app.Services
                .GetRequiredService<IOptions<ScalarConfiguration>>()
                .Value;
            var scalarTitle = scalarConfiguration.Title;
            var moduleRegistry = app.Services.GetService<HttpModuleRegistry>();
            var modules = moduleRegistry?.Modules ?? [];

            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                if (!string.IsNullOrWhiteSpace(scalarTitle))
                {
                    options.WithTitle(scalarTitle);
                }

                options.AddDocuments(modules.Select(static module =>
                    new ScalarDocument(module.Name, module.Title)));
            });
        }

        return app;
    }

    private static bool ShouldInclude(ApiDescription description, string moduleName)
    {
        return description.ActionDescriptor.EndpointMetadata
            .OfType<HttpModule>()
            .Any(module => StringComparer.OrdinalIgnoreCase.Equals(
                module.Name,
                moduleName));
    }
}
