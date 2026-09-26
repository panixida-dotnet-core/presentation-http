using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PANiXiDA.Core.Presentation.Http.Generators.Endpoints;

internal static class ConstructorFactoryBuilder
{
    internal static string? Build(
        SourceProductionContext context,
        Compilation compilation,
        INamedTypeSymbol type,
        DiagnosticDescriptor ambiguousConstructor,
        DiagnosticDescriptor unsupportedConstructor)
    {
        var constructors = type.InstanceConstructors
            .Where(item => item.DeclaredAccessibility == Accessibility.Public)
            .ToArray();
        var attribute = compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute");
        var marked = constructors
            .Where(item => item.GetAttributes()
                .Any(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, attribute)))
            .ToArray();
        IMethodSymbol? constructor = null;
        if (marked.Length == 1)
        {
            constructor = marked[0];
        }
        else if (marked.Length == 0 && constructors.Length == 1)
        {
            constructor = constructors[0];
        }

        if (constructor is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ambiguousConstructor,
                type.Locations[0],
                type.ToDisplayString()));
            return null;
        }

        if (constructor.Parameters.Any(parameter => parameter.RefKind != RefKind.None
                || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
                || parameter.Type.IsRefLikeType
                || !compilation.IsSymbolAccessibleWithin(parameter.Type, compilation.Assembly))
            || HasUninitializedRequiredMembers(type, constructor, compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                unsupportedConstructor,
                constructor.Locations[0],
                type.ToDisplayString()));
            return null;
        }

        var arguments = new List<string>();
        foreach (var parameter in constructor.Parameters)
        {
            var argument = BuildArgument(parameter, compilation);
            if (argument is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    unsupportedConstructor,
                    parameter.Locations[0],
                    type.ToDisplayString()));
                return null;
            }

            arguments.Add(argument);
        }

        return "new " + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            + "(" + string.Join(", ", arguments) + ")";
    }

    private static string? BuildArgument(
        IParameterSymbol parameter,
        Compilation compilation)
    {
        var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var serviceKeyAttribute = compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.DependencyInjection.ServiceKeyAttribute");
        if (parameter.GetAttributes()
            .Any(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, serviceKeyAttribute)))
        {
            return null;
        }

        var keyedAttribute = compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute");
        var keyed = parameter
            .GetAttributes()
            .FirstOrDefault(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, keyedAttribute));
        string resolve;
        if (keyed is not null)
        {
            if (keyed.ConstructorArguments.Length != 1
                || keyed.ConstructorArguments[0].Kind is TypedConstantKind.Error or TypedConstantKind.Array)
            {
                return null;
            }

            var key = keyed.ConstructorArguments[0].ToCSharpString();
            resolve = parameter.HasExplicitDefaultValue
                ? "global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions."
                    + "GetKeyedService(services, typeof(" + typeName + "), " + key + ")"
                : "global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions."
                    + "GetRequiredKeyedService<" + typeName + ">(services, " + key + ")";
        }
        else
        {
            resolve = parameter.HasExplicitDefaultValue
                ? "services.GetService(typeof(" + typeName + "))"
                : "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions."
                    + "GetRequiredService<" + typeName + ">(services)";
        }

        if (!parameter.HasExplicitDefaultValue)
        {
            return resolve;
        }

        var defaultValue = parameter.ExplicitDefaultValue is null
            ? "default(" + typeName + ")"
            : "(" + typeName + ")" + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(
                parameter.ExplicitDefaultValue,
                quoteStrings: true,
                useHexadecimalNumbers: false);
        return "(" + typeName + ")(" + resolve + " ?? (object?)" + defaultValue + ")!";
    }

    private static bool HasUninitializedRequiredMembers(
        INamedTypeSymbol type,
        IMethodSymbol constructor,
        Compilation compilation)
    {
        var attribute = compilation.GetTypeByMetadataName(
            "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
        if (constructor.GetAttributes()
            .Any(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, attribute)))
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current
                .GetMembers()
                .Any(member => member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }))
            {
                return true;
            }
        }

        return false;
    }
}
