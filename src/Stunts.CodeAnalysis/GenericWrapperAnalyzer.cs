using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Reports generic methods that pass their type parameters to generator methods 
    /// but whose stunt types cannot be closed at their call sites.
    /// </summary>
    // Visual Basic only supports run-time stunts, which need no generic wrapper annotations.
#pragma warning disable RS1004
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004
    public class GenericWrapperAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Property in <see cref="StuntDiagnostics.UnannotatedGenericWrapper"/> diagnostics 
        /// containing the full name of the generator attribute to add.
        /// </summary>
        public const string AttributeProperty = "Attribute";

        readonly Type generatorAttribute;

        /// <summary>
        /// Instantiates the analyzer for methods annotated with <see cref="StuntGeneratorAttribute"/>.
        /// </summary>
        public GenericWrapperAnalyzer() : this(typeof(StuntGeneratorAttribute)) { }

        /// <summary>
        /// Customizes the analyzer by specifying a custom generator attribute.
        /// </summary>
        protected GenericWrapperAnalyzer(Type generatorAttribute)
            => this.generatorAttribute = generatorAttribute;

        /// <inheritdoc/>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(
                StuntDiagnostics.UnannotatedGenericWrapper,
                StuntDiagnostics.ContainingTypeParameter,
                StuntDiagnostics.VirtualGenericWrapper,
                StuntDiagnostics.UnboundedGenericWrapper);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(start =>
            {
                // Run-time stunts (Stunts.DynamicProxy) create any type, so wrappers need no annotations.
                if (BuildProperties.CompileTimeStuntsDisabled(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions) ||
                    StuntClosure.Create(start.Compilation, generatorAttribute) is not StuntClosure closure)
                    return;

                start.RegisterOperationAction(operation => AnalyzeInvocation(operation, closure), OperationKind.Invocation);
                start.RegisterSymbolAction(symbol => AnalyzeMethod(symbol, closure), SymbolKind.Method);
            });
        }

        static void AnalyzeInvocation(OperationAnalysisContext context, StuntClosure closure)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var target = invocation.TargetMethod;
            if (!closure.IsGenerator(target))
                return;

            var parameters = new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default);
            foreach (var argument in target.TypeArguments)
                Collect(argument, parameters);

            var attribute = closure.GeneratorAttribute;
            var attributeName = attribute.Name.EndsWith("Attribute", StringComparison.Ordinal)
                ? attribute.Name.Substring(0, attribute.Name.Length - "Attribute".Length)
                : attribute.Name;

            if (parameters.FirstOrDefault(parameter => parameter.TypeParameterKind == TypeParameterKind.Type) is ITypeParameterSymbol containing)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.ContainingTypeParameter,
                    invocation.Syntax.GetLocation(),
                    context.ContainingSymbol.Name,
                    containing.DeclaringType?.Name,
                    attributeName));
                return;
            }

            foreach (var wrapper in parameters
                .Select(parameter => parameter.DeclaringMethod)
                .OfType<IMethodSymbol>()
                .Distinct<IMethodSymbol>(SymbolEqualityComparer.Default)
                .Where(method => !closure.IsGenerator(method)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.UnannotatedGenericWrapper,
                    invocation.Syntax.GetLocation(),
                    wrapper.Locations.Where(location => location.IsInSource),
                    ImmutableDictionary<string, string?>.Empty.Add(AttributeProperty, attribute.ToDisplayString()),
                    wrapper.Name,
                    target.Name,
                    attributeName));
            }
        }

        static void AnalyzeMethod(SymbolAnalysisContext context, StuntClosure closure)
        {
            var method = (IMethodSymbol)context.Symbol;
            if (method.DeclaringSyntaxReferences.IsEmpty || !closure.IsGenerator(method))
                return;

            var result = closure.GetDefinitions(method, context.CancellationToken);
            var diagnostic = result.Error switch
            {
                StuntClosureError.VirtualWrapper => Diagnostic.Create(
                    StuntDiagnostics.VirtualGenericWrapper, method.Locations[0], method.Name),
                StuntClosureError.Recursive => Diagnostic.Create(
                    StuntDiagnostics.UnboundedGenericWrapper, method.Locations[0], method.Name,
                    "it invokes itself recursively with different type arguments"),
                StuntClosureError.TooDeep => Diagnostic.Create(
                    StuntDiagnostics.UnboundedGenericWrapper, method.Locations[0], method.Name,
                    $"the chain of generic wrappers is longer than {StuntClosure.MaxDepth}"),
                _ => null,
            };

            if (diagnostic != null)
                context.ReportDiagnostic(diagnostic);
        }

        static void Collect(ITypeSymbol type, HashSet<ITypeParameterSymbol> parameters)
        {
            switch (type)
            {
                case ITypeParameterSymbol parameter:
                    parameters.Add(parameter);
                    break;
                case IArrayTypeSymbol array:
                    Collect(array.ElementType, parameters);
                    break;
                case IPointerTypeSymbol pointer:
                    Collect(pointer.PointedAtType, parameters);
                    break;
                case INamedTypeSymbol named:
                    foreach (var argument in named.TypeArguments)
                        Collect(argument, parameters);
                    if (named.ContainingType != null)
                        Collect(named.ContainingType, parameters);
                    break;
            }
        }
    }
}
