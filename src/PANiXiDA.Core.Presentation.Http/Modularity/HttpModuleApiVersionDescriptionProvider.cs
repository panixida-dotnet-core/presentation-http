using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using Microsoft.AspNetCore.Mvc.ApiExplorer;

using PANiXiDA.Core.Presentation.Http.Configurations;

namespace PANiXiDA.Core.Presentation.Http.Modularity;

internal sealed class HttpModuleApiVersionDescriptionProvider(
    IApiVersionDescriptionProvider versionProvider,
    IApiDescriptionGroupCollectionProvider descriptionProvider,
    IReadOnlyList<HttpModule> modules) : IApiVersionDescriptionProvider
{
    public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions
    {
        get
        {
            var descriptions = GetDescriptions();
            var versions = versionProvider.ApiVersionDescriptions
                .Where(version => descriptions.Any(description =>
                    !IsUnversioned(description) &&
                    StringComparer.OrdinalIgnoreCase.Equals(description.GroupName, version.GroupName)) ||
                    GetModule(version) is null && descriptions.Any(description =>
                        IsUnversioned(description) && GetModule(description) is null))
                .ToList();

            foreach (var module in modules)
            {
                if (versions.Any(version => GetModule(version) == module) ||
                    !descriptions.Any(description => GetModule(description) == module))
                {
                    continue;
                }

                versions.Add(new ApiVersionDescription(ApiVersion.Neutral, module.Name));
            }

            var documentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var version in versions)
            {
                if (!documentNames.Add(version.GroupName))
                {
                    throw new InvalidOperationException(
                        $"The OpenAPI document name '{version.GroupName}' is already registered. Configure unique HTTP module names.");
                }
            }

            return versions;
        }
    }

    internal bool ShouldInclude(
        ApiDescription description,
        ApiVersionDescription document)
    {
        if (!IsUnversioned(description))
        {
            return StringComparer.OrdinalIgnoreCase.Equals(description.GroupName, document.GroupName);
        }

        var module = GetModule(description);

        if (module != GetModule(document))
        {
            return false;
        }

        return ReferenceEquals(
            description,
            GetDescriptions().First(item =>
                ReferenceEquals(item.ActionDescriptor, description.ActionDescriptor) &&
                item.HttpMethod == description.HttpMethod &&
                item.RelativePath == description.RelativePath));
    }

    internal string GetDocumentTitle(ApiVersionDescription description)
    {
        var module = GetModule(description);

        if (module is null)
        {
            return description.GroupName;
        }

        if (description.ApiVersion == ApiVersion.Neutral)
        {
            return module.Title;
        }

        var version = description.ApiVersion.ToString(ApiVersioningConfiguration.GroupNameFormat);

        return $"{module.Title} {version}";
    }

    private IEnumerable<ApiDescription> GetDescriptions()
    {
        return descriptionProvider.ApiDescriptionGroups.Items
            .SelectMany(static group => group.Items);
    }

    private HttpModule? GetModule(ApiDescription description)
    {
        var name = description.ActionDescriptor.EndpointMetadata
            .OfType<HttpModule>()
            .LastOrDefault()?.Name ?? description.GroupName;

        return modules.FirstOrDefault(module =>
            StringComparer.OrdinalIgnoreCase.Equals(module.Name, name));
    }

    private HttpModule? GetModule(ApiVersionDescription description)
    {
        var version = description.ApiVersion.ToString(ApiVersioningConfiguration.GroupNameFormat);

        return modules.FirstOrDefault(module =>
            StringComparer.OrdinalIgnoreCase.Equals(
                description.ApiVersion == ApiVersion.Neutral ? module.Name : $"{module.Name}-{version}",
                description.GroupName));
    }

    private static bool IsUnversioned(ApiDescription description)
    {
        var metadata = description.ActionDescriptor.EndpointMetadata
            .OfType<ApiVersionMetadata>()
            .LastOrDefault();

        return metadata is null || metadata.IsApiVersionNeutral;
    }
}
