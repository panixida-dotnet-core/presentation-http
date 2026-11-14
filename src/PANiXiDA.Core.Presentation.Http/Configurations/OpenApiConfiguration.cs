using Asp.Versioning.ApiExplorer;
using Asp.Versioning.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using PANiXiDA.Core.ApiDocumentation.DependencyInjection;
using PANiXiDA.Core.Presentation.Http.Modularity;
using PANiXiDA.Core.Presentation.Http.Options.OpenApi;
using PANiXiDA.Core.Presentation.Http.Transformers;

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

        services.AddSingleton<IValidateOptions<OpenApiOptions>, OpenApiOptionsValidator>();
        services.AddOptions<OpenApiOptions>()
            .Bind(configuration.GetSection(OpenApiOptions.SectionName))
            .ValidateOnStart();
        services.AddOutputCache();
        services.AddApiDocumentSource<HttpApiDocumentSource>();
        return services;
    }

    internal static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<OpenApiOptions>>().Value;
        if (options.Enabled ?? app.Environment.IsDevelopment())
        {
            var endpoint = app.MapOpenApi().WithDocumentPerVersion();
            if (options.OpenApiCacheDuration is { } duration)
            {
                endpoint.CacheOutput(policy => policy.Expire(duration).VaryByValue(context =>
                {
                    var request = context.Request;
                    return new("OpenApiServer", UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase));
                }));

                endpoint.Finally(builder =>
                {
                    var generateDocument = builder.RequestDelegate!;
                    var pipeline = ((IApplicationBuilder)app).New();
                    pipeline.UseOutputCache();
                    pipeline.Run(generateDocument);
                    var serveCachedDocument = pipeline.Build();

                    // Use the host's output cache when it has already handled the request.
                    builder.RequestDelegate = context => context.Features.Get<IOutputCacheFeature>() is null
                        ? serveCachedDocument(context)
                        : generateDocument(context);
                });
            }
        }

        return app;
    }
}
