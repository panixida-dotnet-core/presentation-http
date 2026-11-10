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
using PANiXiDA.Core.Presentation.Http.Options.Scalar;
using PANiXiDA.Core.Presentation.Http.Transformers;
using PANiXiDA.Core.ApiDocumentation.DependencyInjection;



using System.Diagnostics.CodeAnalysis;

using ScalarOptions = PANiXiDA.Core.Presentation.Http.Options.Scalar.ScalarOptions;

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

                options.Document.AddOperationTransformer<SortFieldArrayOpenApiOperationTransformer>();
                options.Document.AddOperationTransformer<BearerSecurityOpenApiOperationTransformer>();
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
            serviceProvider.GetRequiredService<IOptions<ApiExplorerOptions>>().Value,
            modules));
        services.Replace(ServiceDescriptor.Singleton<IApiVersionDescriptionProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<HttpModuleApiVersionDescriptionProvider>()));

        services.AddSingleton<IValidateOptions<ScalarOptions>, ScalarOptionsValidator>();
        services.AddOptions<ScalarOptions>()
            .Bind(configuration.GetSection(ScalarOptions.SectionName))
            .ValidateOnStart();
        services.AddApiDocumentSource<HttpApiDocumentSource>();
        return services;
    }

    internal static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        var scalarOptions = app.Services.GetRequiredService<IOptions<ScalarOptions>>().Value;
        if (scalarOptions.Enabled ?? app.Environment.IsDevelopment())
        {
            app.MapOpenApi()
                .WithDocumentPerVersion();
        }

        return app;
    }
}
