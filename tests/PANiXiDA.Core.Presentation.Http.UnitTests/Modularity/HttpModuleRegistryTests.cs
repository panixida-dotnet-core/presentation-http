using Microsoft.Extensions.Configuration;

using PANiXiDA.Core.Presentation.Http.Modularity;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Modularity;

public sealed class HttpModuleRegistryTests
{
    [Fact(DisplayName = "HTTP module registry creates module metadata from configuration")]
    public void Constructor_ShouldCreateModuleFromConfiguration()
    {
        var assembly = typeof(HttpModuleRegistryTests).Assembly;
        var assemblyName = assembly.GetName().Name;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"HttpModules:{assemblyName}:Name"] = "tests",
                [$"HttpModules:{assemblyName}:Title"] = "Test endpoints"
            })
            .Build();

        var registry = new HttpModuleRegistry(configuration, [assembly]);

        var module = registry.Modules.ShouldHaveSingleItem();
        module.Name.ShouldBe("tests");
        module.Title.ShouldBe("Test endpoints");
        module.PresentationAssembly.ShouldBeSameAs(assembly);
        registry.TryGetModule(assembly, out var registeredModule).ShouldBeTrue();
        registeredModule.ShouldBeSameAs(module);
    }

    [Fact(DisplayName = "HTTP module registry allows no modules without configuration")]
    public void Constructor_ShouldAllowNoModulesWithoutConfiguration()
    {
        var configuration = new ConfigurationBuilder().Build();

        var registry = new HttpModuleRegistry(configuration, []);

        registry.Modules.ShouldBeEmpty();
        registry.TryGetModule(typeof(HttpModuleRegistryTests).Assembly, out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "HTTP module registry requires configuration")]
    public void Constructor_ShouldRequireConfiguration()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = new HttpModuleRegistry(null!, []);
        });

        exception.ParamName.ShouldBe("configuration");
    }

    [Fact(DisplayName = "HTTP module registry requires a presentation assembly collection")]
    public void Constructor_ShouldRequirePresentationAssemblies()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            _ = new HttpModuleRegistry(configuration, null!);
        });

        exception.ParamName.ShouldBe("presentationAssemblies");
    }

    [Fact(DisplayName = "HTTP module registry requires a configuration section for every assembly")]
    public void Constructor_ShouldRequireModuleConfigurationSection()
    {
        var configuration = new ConfigurationBuilder().Build();
        var assembly = typeof(HttpModuleRegistryTests).Assembly;

        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            _ = new HttpModuleRegistry(configuration, [assembly]);
        });

        exception.Message.ShouldContain($"HttpModules:{assembly.GetName().Name}");
    }

    [Theory(DisplayName = "HTTP module registry requires module names and titles")]
    [InlineData("Name")]
    [InlineData("Title")]
    public void Constructor_ShouldRequireModuleConfigurationValue(string missingKey)
    {
        var assembly = typeof(HttpModuleRegistryTests).Assembly;
        var assemblyName = assembly.GetName().Name;
        var values = new Dictionary<string, string?>
        {
            [$"HttpModules:{assemblyName}:Name"] = "tests",
            [$"HttpModules:{assemblyName}:Title"] = "Test endpoints"
        };
        values[$"HttpModules:{assemblyName}:{missingKey}"] = " ";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            _ = new HttpModuleRegistry(configuration, [assembly]);
        });

        exception.Message.ShouldContain($"HttpModules:{assemblyName}:{missingKey}");
    }
}
