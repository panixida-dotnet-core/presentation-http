using Asp.Versioning.ApiExplorer;
using Asp.Versioning.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.Presentation.Http.Modularity;
using PANiXiDA.Core.Presentation.Http.Transformers;

using Scalar.AspNetCore;

using System.Diagnostics.CodeAnalysis;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class OpenApiConfiguration
{
    [RequiresUnreferencedCode(ApiVersioningConfiguration.TrimmingMessage)]
    internal static IServiceCollection AddOpenApiConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<HttpModule> modules)
    {
        services.AddApiVersioning()
            .AddOpenApi();
        services.AddSingleton<IConfigureOptions<VersionedOpenApiOptions>>(serviceProvider =>
            new ConfigureNamedOptions<VersionedOpenApiOptions>(
                null,
                options =>
            {
                var documents = serviceProvider.GetRequiredService<HttpModuleApiVersionDescriptionProvider>();
                options.Document.AddScalarTransformers();
                options.Document.AddOperationTransformer<SortFieldArrayOpenApiOperationTransformer>();
                options.Document.ShouldInclude = description =>
                    documents.ShouldInclude(description, options.Description);
                options.Document.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = documents.GetDocumentTitle(options.Description);

                    return Task.CompletedTask;
                });
            }));

        services.AddSingleton(serviceProvider => new HttpModuleApiVersionDescriptionProvider(
            serviceProvider.GetRequiredService<IApiVersionDescriptionProviderFactory>()
                .Create(serviceProvider.GetRequiredService<EndpointDataSource>()),
            serviceProvider.GetRequiredService<IApiDescriptionGroupCollectionProvider>(),
            modules));
        services.Replace(ServiceDescriptor.Singleton<IApiVersionDescriptionProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<HttpModuleApiVersionDescriptionProvider>()));

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
            app.MapOpenApi()
                .WithDocumentPerVersion();
            app.MapScalarApiReference(options =>
            {
                if (!string.IsNullOrWhiteSpace(scalarTitle))
                {
                    options.WithTitle(scalarTitle);
                }

                var documents = app.Services.GetRequiredService<HttpModuleApiVersionDescriptionProvider>();
                options.AddDocuments(documents.ApiVersionDescriptions.Select(description =>
                    new ScalarDocument(
                        description.GroupName,
                        documents.GetDocumentTitle(description))));
            });
        }

        return app;
    }
}
