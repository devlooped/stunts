using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts.CodeAnalysis
{
    static class GenericFactory
    {
        public static bool UsesTemplate(IMethodSymbol method, Compilation compilation, INamedTypeSymbol attribute, CancellationToken cancellationToken)
        {
            foreach (var reference in method.OriginalDefinition.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax(cancellationToken);
                var model = compilation.GetSemanticModel(syntax.SyntaxTree);
                if (model.GetOperation(syntax, cancellationToken) is not IOperation operation)
                    continue;
                var usesTemplate = false;
                var needsCallerTypes = false;
                Inspect(operation, attribute, ref usesTemplate, ref needsCallerTypes);
                if (usesTemplate && !needsCallerTypes)
                    return true;
            }

            return false;
        }

        static void Inspect(IOperation operation, INamedTypeSymbol attribute, ref bool usesTemplate, ref bool needsCallerTypes)
        {
            if (operation is IInvocationOperation invocation &&
                invocation.TargetMethod.GetAttributes().Any(value => SymbolEqualityComparer.Default.Equals(value.AttributeClass, attribute)))
            {
                usesTemplate |= invocation.TargetMethod.TypeArguments.OfType<INamedTypeSymbol>().Any(ContainsParameter);
                needsCallerTypes |= invocation.TargetMethod.TypeArguments.Any(type => type is ITypeParameterSymbol);
            }
            foreach (var child in operation.ChildOperations)
                Inspect(child, attribute, ref usesTemplate, ref needsCallerTypes);
        }

        static bool ContainsParameter(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsParameter(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(ContainsParameter) ||
                named.ContainingType != null && ContainsParameter(named.ContainingType),
            _ => false,
        };
    }
}
