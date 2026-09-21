using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using PANiXiDA.Core.Presentation.Http.Endpoints;
using PANiXiDA.Core.Presentation.Http.Generators;

using System.Runtime.Loader;

namespace PANiXiDA.Core.Presentation.Http.UnitTests.Generators;

public sealed class EndpointRegistrationGeneratorTests
{
    private static readonly MetadataReference[] References =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))];

    [Fact(DisplayName = "Generated endpoint registrations replace discovery and activation with direct factories")]
    public void Generate_ShouldCreateDirectFactories()
    {
        const string source = """
            using Asp.Versioning;
            using Microsoft.AspNetCore.Routing;
            using PANiXiDA.Core.Presentation.Http.Endpoints;
            public sealed class Dependency { }
            public sealed class Group(Dependency dependency) : IEndpointGroup
            {
                public string Route => "/test";
                public string Name => "Test";
                public ApiVersion ApiVersion => new(1, 0);
                public void Map(IEndpointRouteBuilder endpoints) { }
            }
            public sealed class Endpoint(Dependency dependency) : IEndpoint<Group>
            {
                public string Route => "/";
                public string Name => "Get";
                public string Summary => "Gets a value.";
                public void Map(EndpointMapBuilder builder) { }
            }
            """;

        var (result, _) = Compile(source);
        var generated = result.GeneratedTrees.Single().GetText(TestContext.Current.CancellationToken).ToString();

        generated.ShouldContain("new global::Group(");
        generated.ShouldContain("new global::Endpoint(");
        generated.ShouldContain("GetRequiredService<global::Dependency>");
        generated.ShouldNotContain("GetTypes(");
        generated.ShouldNotContain("GetInterfaces(");
        generated.ShouldNotContain("ActivatorUtilities.CreateInstance");
    }

    [Fact(DisplayName = "Endpoint generator discovers inherited contracts and deduplicates partial types in runtime name order")]
    public void Generate_ShouldPreserveDiscoveryRules()
    {
        const string source = """
            using Asp.Versioning;
            using Microsoft.AspNetCore.Routing;
            using PANiXiDA.Core.Presentation.Http.Endpoints;
            public abstract class GroupBase : IEndpointGroup
            {
                public string Route => "/";
                public string Name => "Group";
                public ApiVersion ApiVersion => new(1, 0);
                public void Map(IEndpointRouteBuilder endpoints) { }
            }
            public class ZGroup : GroupBase { }
            public class Container { internal class Nested : GroupBase { } }
            public class ContainerAfter : GroupBase { }
            public abstract class EndpointBase<T> : IEndpoint<T> where T : IEndpointGroup
            {
                public string Route => "/";
                public string Name => "Get";
                public string Summary => "Gets a value.";
                public void Map(EndpointMapBuilder builder) { }
            }
            public partial class AEndpoint : EndpointBase<ZGroup> { }
            public partial class AEndpoint : EndpointBase<ZGroup> { }
            public class ZEndpoint : EndpointBase<ZGroup> { }
            public interface IUnused : IEndpoint<ZGroup> { }
            public class Unrelated { }
            """;

        var (result, _) = Compile(source);
        var generated = result.GeneratedTrees.Single().GetText(TestContext.Current.CancellationToken).ToString();

        generated.ShouldNotContain("new global::GroupBase");
        generated.ShouldNotContain("new global::Unrelated");
        generated.Split("new global::AEndpoint()").Length.ShouldBe(2);
        generated.IndexOf("new global::AEndpoint()", StringComparison.Ordinal)
            .ShouldBeLessThan(generated.IndexOf("new global::ZEndpoint()", StringComparison.Ordinal));
        generated.IndexOf("new global::Container.Nested()", StringComparison.Ordinal)
            .ShouldBeLessThan(generated.IndexOf("new global::ContainerAfter()", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "Endpoint generator diagnoses unsupported group types and constructors")]
    [InlineData("private class Group : Base { }", "public class Container {", "}", "PANHTTPSG001")]
    [InlineData("file class Group : Base { }", "", "", "PANHTTPSG001")]
    [InlineData("public class Group<T> : Base { }", "", "", "PANHTTPSG001")]
    [InlineData("public class Group : Base { private Group() { } }", "", "", "PANHTTPSG002")]
    [InlineData("public class Group : Base { public Group() { } public Group(string value) { } }", "", "", "PANHTTPSG002")]
    [InlineData("public class Group : Base { public Group(ref int value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { public Group(System.Span<int> value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public unsafe class Group : Base { public Group(int* value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public unsafe class Group : Base { public Group(delegate*<void> value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { public required string Value { get; init; } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { public Group([Microsoft.Extensions.DependencyInjection.ServiceKey] object key) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { public Group([Microsoft.Extensions.DependencyInjection.FromKeyedServices] object value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { public Group([Microsoft.Extensions.DependencyInjection.FromKeyedServices(new int[] { 1 })] object value) { } }", "", "", "PANHTTPSG003")]
    [InlineData("public class Group : Base { [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor] public Group() { } [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor] public Group(string value) { } }", "", "", "PANHTTPSG002")]
    public void Generate_ShouldDiagnoseUnsupportedTypes(string declaration, string prefix, string suffix, string diagnostic)
    {
        var source = GroupBaseSource + prefix + declaration + suffix;

        var (result, _) = Compile(source, diagnostic);

        result.Diagnostics.Single().Id.ShouldBe(diagnostic);
    }

    [Fact(DisplayName = "Endpoint generator rejects ref-like groups without generating invalid generic registrations")]
    public void Generate_ShouldRejectRefLikeGroup()
    {
        const string source = """
            public ref struct Group : PANiXiDA.Core.Presentation.Http.Endpoints.IEndpointGroup
            {
                public string Route => "/";
                public string Name => "Group";
                public Asp.Versioning.ApiVersion ApiVersion => new(1, 0);
                public void Map(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints) { }
            }
            """;

        var (result, _) = Compile(source, "PANHTTPSG001");

        result.GeneratedTrees.Single().GetText(TestContext.Current.CancellationToken).ToString().ShouldNotContain("typeof(global::Group)");
    }

    [Theory(DisplayName = "Endpoint generator diagnoses incomplete constructor code without crashing during editing")]
    [InlineData("public Group([Microsoft.Extensions.DependencyInjection.FromKeyedServices(Unknown)] object value) { }", "CS0103")]
    [InlineData("private class Dependency { } public Group(Dependency value) { }", "CS0051")]
    public void Generate_ShouldHandleInvalidConstructorSource(string members, string compilerDiagnostic)
    {
        var source = GroupBaseSource + "public class Group : Base { " + members + " }";

        var (result, _) = Compile(source, "PANHTTPSG003", expectedCompilerDiagnostic: compilerDiagnostic);

        result.Results.Single().Exception.ShouldBeNull();
    }

    [Theory(DisplayName = "Generated factories initialize an untouched assembly and resolve keyed and optional constructor dependencies")]
    [InlineData(false, "services3fallback")]
    [InlineData(true, "registered3fallback")]
    public void GeneratedRegistration_ShouldInitializeAssemblyAndResolveDependencies(bool registerOverride, string expected)
    {
        var source = GroupBaseSource + """
            public sealed class Group : Base
            {
                private readonly System.Collections.Generic.List<string> calls;
                public Group() { throw new System.InvalidOperationException("Wrong constructor"); }
                [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
                public Group([Microsoft.Extensions.DependencyInjection.FromKeyedServices("calls")] System.Collections.Generic.List<string> calls, string tag = "services", int count = 3, [Microsoft.Extensions.DependencyInjection.FromKeyedServices("missing")] string keyed = "fallback")
                {
                    this.calls = calls;
                    calls.Add(tag + count + keyed);
                }
                public override void Map(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
                {
                    calls.Add("mapped");
                }
            }
            """;
        var (_, compilation) = Compile(source);
        using var stream = new MemoryStream();
        compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        stream.Position = 0;
        var loadContext = new AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
        var calls = new List<string>();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddKeyedSingleton("calls", calls);
        if (registerOverride)
        {
            builder.Services.AddSingleton("registered");
        }
        using var app = builder.Build();

        try
        {
            var assembly = loadContext.LoadFromStream(stream);

            EndpointRegistry.MapGroups(app, assembly);

            calls.ShouldBe([expected, "mapped"]);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [Fact(DisplayName = "Endpoint generator ignores projects without the HTTP runtime contract")]
    public void Generate_ShouldIgnoreMissingRuntime()
    {
        var references = References.Where(reference => !reference.Display!.EndsWith("PANiXiDA.Core.Presentation.Http.dll", StringComparison.OrdinalIgnoreCase));
        var compilation = CSharpCompilation.Create("NoHttp", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new EndpointRegistrationGenerator())
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        driver.GetRunResult().GeneratedTrees.ShouldBeEmpty();
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
    }

    [Fact(DisplayName = "Endpoint generator compares contract symbols instead of accepting identically named foreign interfaces")]
    public void Generate_ShouldIgnoreForeignContracts()
    {
        var foreign = CSharpCompilation.Create("ForeignHttp", [CSharpSyntaxTree.ParseText("""
            namespace PANiXiDA.Core.Presentation.Http.Endpoints
            {
                public interface IEndpointGroup { }
                public interface IEndpoint<T> { }
            }
            """, cancellationToken: TestContext.Current.CancellationToken)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        foreign.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        var reference = MetadataReference.CreateFromImage(stream.ToArray(), new MetadataReferenceProperties(aliases: ["foreign"]));

        var (result, _) = Compile("""
            extern alias foreign;
            public class Group : foreign::PANiXiDA.Core.Presentation.Http.Endpoints.IEndpointGroup { }
            public class Endpoint : foreign::PANiXiDA.Core.Presentation.Http.Endpoints.IEndpoint<Group> { }
            """, additionalReference: reference);

        var generated = result.GeneratedTrees.Single().GetText(TestContext.Current.CancellationToken).ToString();
        generated.ShouldNotContain("new global::Group(");
        generated.ShouldNotContain("new global::Endpoint(");
    }

    [Fact(DisplayName = "Endpoint generator rejects endpoints targeting a group from another assembly")]
    public void Generate_ShouldRejectExternalGroup()
    {
        const string source = """
            public class Endpoint : PANiXiDA.Core.Presentation.Http.Endpoints.IEndpoint<PANiXiDA.Core.Presentation.Http.UnitTests.Endpoints.Fixtures.Groups.OtherEndpointGroup>
            {
                public string Route => "/";
                public string Name => "External";
                public string Summary => "External group.";
                public void Map(PANiXiDA.Core.Presentation.Http.Endpoints.EndpointMapBuilder builder) { }
            }
            """;

        var (result, _) = Compile(source, "PANHTTPSG004");

        result.Diagnostics.Single().GetMessage().ShouldContain("must be declared in the same assembly");
    }

    [Fact(DisplayName = "Endpoint generator supports escaped type names and constructors initializing required members")]
    public void Generate_ShouldSupportEscapedTypesAndRequiredMembers()
    {
        var source = GroupBaseSource + """
            namespace @event
            {
                public class @class : Base
                {
                    public required string NameOverride { get; init; }
                    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                    public @class(string? value = null) { NameOverride = value ?? "default"; }
                }
            }
            """;

        var (result, _) = Compile(source);

        result.GeneratedTrees.Single().GetText(TestContext.Current.CancellationToken).ToString().ShouldContain("new global::@event.@class(");
    }

    private const string GroupBaseSource = """
        public abstract class Base : PANiXiDA.Core.Presentation.Http.Endpoints.IEndpointGroup
        {
            public string Route => "/";
            public string Name => "Group";
            public Asp.Versioning.ApiVersion ApiVersion => new(1, 0);
            public virtual void Map(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints) { }
        }
        """;

    private static (GeneratorDriverRunResult Result, Compilation Compilation) Compile(string source, string? expectedDiagnostic = null,
        MetadataReference? additionalReference = null, string? expectedCompilerDiagnostic = null)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("GeneratedEndpoints_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: TestContext.Current.CancellationToken)],
            additionalReference is null ? References : References.Append(additionalReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create([new EndpointRegistrationGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);
        if (expectedDiagnostic is null)
        {
            diagnostics.ShouldBeEmpty();
        }
        else
        {
            diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe([expectedDiagnostic]);
        }
        var compilerErrors = updated.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.Id);
        compilerErrors.ShouldBe(expectedCompilerDiagnostic is null ? [] : [expectedCompilerDiagnostic]);
        return (driver.GetRunResult(), updated);
    }
}
