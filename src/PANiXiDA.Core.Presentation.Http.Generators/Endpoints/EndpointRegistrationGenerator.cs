using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Immutable;
using System.Text;

namespace PANiXiDA.Core.Presentation.Http.Generators.Endpoints;

/// <summary>
/// Generates endpoint discovery results and constructor factories for the consuming assembly.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EndpointRegistrationGenerator : IIncrementalGenerator
{
    private const string RegistryName = "PANiXiDA.Core.Presentation.Http.Endpoints.EndpointRegistry";
    private const string ContractsNamespace = "PANiXiDA.Core.Presentation.Http.Endpoints.";
    private const string DiagnosticCategory = "EndpointRegistration";

    private static readonly DiagnosticDescriptor UnsupportedType = new(
        "PANHTTPSG001",
        "Endpoint registration requires accessible closed types",
        "Endpoint or group '{0}' must be a non-generic type accessible from generated code; private, protected and file-local types are not supported",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "PANHTTPSG002",
        "Endpoint activation requires an unambiguous constructor",
        "Endpoint or group '{0}' must have one public constructor or exactly one public constructor marked with ActivatorUtilitiesConstructorAttribute",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnsupportedConstructor = new(
        "PANHTTPSG003",
        "Endpoint constructor cannot be generated",
        "Constructor of '{0}' must have accessible by-value parameter types and initialize required members; keyed service keys must be compile-time constants",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ExternalGroup = new(
        "PANHTTPSG004",
        "Endpoint and group must share an assembly",
        "Endpoint '{0}' and group '{1}' must be declared in the same assembly",
        DiagnosticCategory,
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Registers semantic discovery of concrete endpoint and group implementations.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                static (syntax, token) => syntax.SemanticModel.GetDeclaredSymbol(
                    syntax.Node,
                    token) as INamedTypeSymbol)
            .Where(static type => type is { TypeKind: TypeKind.Class or TypeKind.Struct, IsAbstract: false })
            .Collect();

        context.RegisterSourceOutput(
            types.Combine(context.CompilationProvider),
            static (output, input) => Generate(output, input.Left, input.Right));
    }

    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<INamedTypeSymbol?> candidates,
        Compilation compilation)
    {
        var registry = compilation.GetTypeByMetadataName(RegistryName);
        var groupContract = compilation.GetTypeByMetadataName(ContractsNamespace + "IEndpointGroup");
        var endpointContract = compilation.GetTypeByMetadataName(ContractsNamespace + "IEndpoint`1");
        if (registry is null || groupContract is null || endpointContract is null)
        {
            return;
        }

        var groups = new List<INamedTypeSymbol>();
        var endpoints = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var factories = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var candidate in candidates.OrderBy(type => GetRuntimeName(type!), StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var type = candidate!;
            if (!seen.Add(type))
            {
                continue;
            }

            var isGroup = type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, groupContract));
            var contracts = type.AllInterfaces
                .Where(item => SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, endpointContract))
                .ToArray();
            if (!isGroup && contracts.Length == 0)
            {
                continue;
            }

            if (!IsSupportedType(type, compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedType,
                    type.Locations[0],
                    type.ToDisplayString()));
                continue;
            }

            var factory = ConstructorFactoryBuilder.Build(
                context,
                compilation,
                type,
                AmbiguousConstructor,
                UnsupportedConstructor);
            if (factory is null)
            {
                continue;
            }

            factories.Add(type, factory);
            if (isGroup)
            {
                groups.Add(type);
            }

            AddEndpoints(context, compilation, type, contracts, endpoints);
        }

        context.AddSource(
            "EndpointRegistrations.g.cs",
            SourceText.From(BuildSource(groups, endpoints, factories), Encoding.UTF8));
    }

    private static void AddEndpoints(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol type,
        INamedTypeSymbol[] contracts,
        Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> endpoints)
    {
        foreach (var contract in contracts)
        {
            var group = (INamedTypeSymbol)contract.TypeArguments[0];
            if (!SymbolEqualityComparer.Default.Equals(group.ContainingAssembly, compilation.Assembly))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ExternalGroup,
                    type.Locations[0],
                    type.ToDisplayString(),
                    group.ToDisplayString()));
                continue;
            }

            if (!endpoints.TryGetValue(group, out var groupEndpoints))
            {
                groupEndpoints = [];
                endpoints.Add(group, groupEndpoints);
            }

            groupEndpoints.Add(type);
        }
    }

    private static bool IsSupportedType(
        INamedTypeSymbol type,
        Compilation compilation)
    {
        if (type.IsRefLikeType)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0 || current.IsFileLocal)
            {
                return false;
            }
        }

        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
    }

    private static string GetRuntimeName(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
        {
            return GetRuntimeName(type.ContainingType) + "+" + type.MetadataName;
        }

        return type.ContainingNamespace.IsGlobalNamespace
            ? type.MetadataName
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
    }

    private static string BuildSource(
        List<INamedTypeSymbol> groups,
        Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> endpoints,
        Dictionary<INamedTypeSymbol, string> factories)
    {
        var mapGroups = new StringBuilder();
        var createGroups = new StringBuilder();
        var createEndpoints = new StringBuilder();
        foreach (var group in groups)
        {
            var name = group.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            mapGroups
                .Append("        ((global::")
                .Append(ContractsNamespace)
                .Append("IEndpointGroup)")
                .Append(factories[group])
                .AppendLine(").Map(endpoints);");
            createGroups
                .Append("        if (groupType == typeof(")
                .Append(name)
                .AppendLine("))")
                .AppendLine("        {")
                .Append("            return ")
                .Append(factories[group])
                .AppendLine(";")
                .AppendLine("        }");
        }

        foreach (var entry in endpoints.OrderBy(item => GetRuntimeName(item.Key), StringComparer.Ordinal))
        {
            createEndpoints
                .Append("        if (groupType == typeof(")
                .Append(entry.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .AppendLine("))")
                .AppendLine("        {")
                .Append("            return new global::")
                .Append(ContractsNamespace)
                .AppendLine("IEndpoint[]")
                .AppendLine("            {");
            foreach (var endpoint in entry.Value)
            {
                createEndpoints
                    .Append("                ")
                    .Append(factories[endpoint])
                    .AppendLine(",");
            }

            createEndpoints
                .AppendLine("            };")
                .AppendLine("        }");
        }

        return $$"""
            // <auto-generated />
            #nullable enable
            #pragma warning disable CA2255

            file static class GeneratedEndpointRegistrations
            {
                [global::System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Initialize()
                {
                    global::{{RegistryName}}.RegisterAssembly(
                        typeof(GeneratedEndpointRegistrations).Assembly,
                        MapGroups,
                        CreateGroup,
                        CreateEndpoints);
                }

                private static void MapGroups(global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
                {
                    var services = endpoints.ServiceProvider;
            {{mapGroups}}
                }

                private static global::{{ContractsNamespace}}IEndpointGroup CreateGroup(
                    global::System.Type groupType,
                    global::System.IServiceProvider services)
                {
            {{createGroups}}
                    throw new global::System.InvalidOperationException(
                        $"Generated factory was not found for endpoint group '{groupType.FullName}'.");
                }

                private static global::System.Collections.Generic.IReadOnlyList<global::{{ContractsNamespace}}IEndpoint> CreateEndpoints(
                    global::System.Type groupType,
                    global::System.IServiceProvider services)
                {
            {{createEndpoints}}
                    return global::System.Array.Empty<global::{{ContractsNamespace}}IEndpoint>();
                }
            }
            """;
    }
}
