using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.Configuration;

namespace PANiXiDA.Core.Presentation.Http.Modularity;

internal sealed class HttpModuleRegistry
{
    private const string ConfigurationSectionName = "HttpModules";

    private readonly Dictionary<Assembly, HttpModule> modulesByAssembly;

    internal HttpModuleRegistry(
        IConfiguration configuration,
        IReadOnlyCollection<Assembly> presentationAssemblies)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(presentationAssemblies);

        var documentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        modulesByAssembly = [];
        var registeredModules = new List<HttpModule>(presentationAssemblies.Count);
        var modulesSection = configuration.GetSection(ConfigurationSectionName);

        foreach (var presentationAssembly in presentationAssemblies)
        {
            if (presentationAssembly is null)
            {
                throw new ArgumentException(
                    "HTTP module assemblies cannot contain null values.",
                    nameof(presentationAssemblies));
            }

            if (modulesByAssembly.ContainsKey(presentationAssembly))
            {
                throw new ArgumentException(
                    $"The presentation assembly '{presentationAssembly.FullName}' is already registered for another HTTP module.",
                    nameof(presentationAssemblies));
            }

            var assemblyName = presentationAssembly.GetName().Name!;
            var moduleSection = modulesSection.GetSection(assemblyName);

            if (!moduleSection.Exists())
            {
                throw new InvalidOperationException(
                    $"Configuration section '{moduleSection.Path}' is required.");
            }

            var name = GetRequiredValue(moduleSection, nameof(HttpModule.Name));
            var title = GetRequiredValue(moduleSection, nameof(HttpModule.Title));

            if (!documentNames.Add(name))
            {
                throw new ArgumentException(
                    $"The OpenAPI document name '{name}' is already registered.",
                    nameof(presentationAssemblies));
            }

            var module = new HttpModule(name, title, presentationAssembly);

            modulesByAssembly.Add(presentationAssembly, module);
            registeredModules.Add(module);
        }

        Modules = registeredModules.AsReadOnly();
    }

    internal IReadOnlyList<HttpModule> Modules { get; }

    internal bool TryGetModule(
        Assembly presentationAssembly,
        [NotNullWhen(true)] out HttpModule? module)
    {
        return modulesByAssembly.TryGetValue(presentationAssembly, out module);
    }

    private static string GetRequiredValue(
        IConfigurationSection section,
        string key)
    {
        var value = section[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{section.Path}:{key}' is required.");
        }

        return value;
    }
}
