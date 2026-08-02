using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Modularity;

internal sealed class HttpModuleRegistry
{
    private readonly Dictionary<Assembly, HttpModule> modulesByAssembly;

    internal HttpModuleRegistry(IReadOnlyCollection<HttpModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var documentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        modulesByAssembly = [];
        var registeredModules = new List<HttpModule>(modules.Count);

        foreach (var module in modules)
        {
            if (module is null)
            {
                throw new ArgumentException(
                    "HTTP modules cannot contain null values.",
                    nameof(modules));
            }

            if (!documentNames.Add(module.Name))
            {
                throw new ArgumentException(
                    $"The OpenAPI document name '{module.Name}' is already registered.",
                    nameof(modules));
            }

            if (!modulesByAssembly.TryAdd(module.PresentationAssembly, module))
            {
                throw new ArgumentException(
                    $"The presentation assembly '{module.PresentationAssembly.FullName}' is already registered for another HTTP module.",
                    nameof(modules));
            }

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
}
