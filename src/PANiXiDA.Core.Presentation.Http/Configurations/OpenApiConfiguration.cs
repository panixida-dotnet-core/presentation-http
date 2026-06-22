using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Scalar.AspNetCore;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class OpenApiConfiguration
{
    internal static IServiceCollection AddOpenApiConfiguration(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddOpenApi(options =>
        {
            options.AddScalarTransformers();
        });

        services.AddOptions<ScalarApiReferenceConfiguration>();

        if (configuration is not null)
        {
            services.Configure<ScalarApiReferenceConfiguration>(
                configuration.GetSection(ScalarApiReferenceConfiguration.SectionName));
        }

        return services;
    }

    internal static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            var scalarConfiguration = app.Services
                .GetRequiredService<IOptions<ScalarApiReferenceConfiguration>>()
                .Value;
            var scalarTitle = scalarConfiguration.Title;

            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                if (!string.IsNullOrWhiteSpace(scalarTitle))
                {
                    options.WithTitle(scalarTitle);
                }
            });
        }

        return app;
    }
}
