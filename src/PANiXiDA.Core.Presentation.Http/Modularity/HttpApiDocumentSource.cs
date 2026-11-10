using Asp.Versioning;

using PANiXiDA.Core.ApiDocumentation;

namespace PANiXiDA.Core.Presentation.Http.Modularity;

internal sealed class HttpApiDocumentSource(HttpModuleApiVersionDescriptionProvider provider) : IApiDocumentSource
{
    public IReadOnlyCollection<ApiDocumentDescriptor> GetDocuments()
    {
        return provider.ApiVersionDescriptions.Select(description => new ApiDocumentDescriptor(
            description.GroupName,
            provider.GetDocumentTitle(description),
            provider.GetDocumentModuleName(description),
            description.ApiVersion == ApiVersion.Neutral ? "unversioned" : description.ApiVersion.ToString(),
            ApiDocumentFormat.OpenApi,
            deprecated: description.IsDeprecated)).ToArray();
    }
}
