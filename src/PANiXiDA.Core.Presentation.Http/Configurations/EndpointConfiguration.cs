using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Modularity;

using System.Reflection;

namespace PANiXiDA.Core.Presentation.Http.Configurations;

internal static class EndpointConfiguration
{
    internal static WebApplication UseEndpointConfiguration(
        this WebApplication app,
        params Assembly[] assemblies)
    {
        var mappedAssemblies = new HashSet<Assembly>();
        var moduleRegistry = app.Services.GetRequiredService<HttpModuleRegistry>();
        var moduleAssemblies = moduleRegistry.Modules.Select(
            static module => module.PresentationAssembly);

        foreach (var presentationAssembly in moduleAssemblies)
        {
            EndpointRegistry.MapGroups(app, presentationAssembly);
            mappedAssemblies.Add(presentationAssembly);
        }

        foreach (var assembly in assemblies)
        {
            if (!mappedAssemblies.Add(assembly))
            {
                continue;
            }

            EndpointRegistry.MapGroups(app, assembly);
        }

        return app;
    }
}
