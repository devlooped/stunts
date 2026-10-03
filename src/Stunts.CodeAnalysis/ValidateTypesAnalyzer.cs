using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Analyzes source code looking for method invocations to methods annotated with 
    /// the <see cref="StuntGeneratorAttribute"/> and reports unsupported scenarios.
    /// </summary>
    // TODO: F#
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ValidateTypesAnalyzer : DiagnosticAnalyzer
    {
        Type generatorAttribute;

        /// <summary>
        /// Instantiates the analyzer to validate invocations to methods 
        /// annotated with <see cref="StuntGeneratorAttribute"/>.
        /// </summary>
        public ValidateTypesAnalyzer() : this(typeof(StuntGeneratorAttribute)) { }

        /// <summary>
        /// Customizes the analyzer by specifying a custom <see cref="generatorAttribute"/> 
        /// to lookup in method invocations.
        /// </summary>
        protected ValidateTypesAnalyzer(Type generatorAttribute)
            => this.generatorAttribute = generatorAttribute;

        /// <summary>
        /// Returns the single <see cref="StuntDiagnostics.BaseTypeNotFirst"/> 
        /// diagnostic this analyzer supports.
        /// </summary>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(
                StuntDiagnostics.BaseTypeNotFirst,
                StuntDiagnostics.DuplicateBaseType,
                StuntDiagnostics.SealedBaseType,
                StuntDiagnostics.EnumType,
                StuntDiagnostics.ContainingTypeNotPartial,
                StuntDiagnostics.DelegateWithOtherTypes,
                StuntDiagnostics.InaccessibleInterfaceMember,
                StuntDiagnostics.UnsupportedRuntimeSignature);

        /// <summary>
        /// Registers the analyzer to take action on method invocation expressions.
        /// </summary>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(start =>
            {
                if (StuntClosure.Create(start.Compilation, generatorAttribute) is StuntClosure closure)
                    start.RegisterOperationAction(operation => AnalyzeOperation(operation, closure), OperationKind.Invocation);
            });
        }

        void AnalyzeOperation(OperationAnalysisContext context, StuntClosure closure)
        {
            var invocation = (IInvocationOperation)context.Operation;
            if (!closure.IsGenerator(invocation.TargetMethod))
                return;

            // Generic wrappers are validated with the stunt types they create, not their type arguments.
            foreach (var (types, _) in closure.Close(invocation.TargetMethod, context.CancellationToken))
                Validate(context, invocation, types);
        }

        static void Validate(OperationAnalysisContext context, IInvocationOperation invocation, ImmutableArray<ITypeSymbol> types)
        {
            foreach (var type in types.OfType<INamedTypeSymbol>())
            {
                if (InterfaceImplementation.InaccessibleMember(type, context.Compilation) is ISymbol member)
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.InaccessibleInterfaceMember,
                        invocation.Syntax.GetLocation(),
                        type.Name,
                        member.ToDisplayString()));
                if (RuntimeSignature.UnsupportedMember(type, context.Compilation) is ISymbol unsupported)
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.UnsupportedRuntimeSignature,
                        invocation.Syntax.GetLocation(), type.Name, unsupported.ToDisplayString()));
            }

            foreach (var enumType in types.Where(x => x.TypeKind == TypeKind.Enum))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.EnumType,
                    invocation.Syntax.GetLocation(),
                    enumType.Name));
            }

            var delegateTypes = types.Where(x => x.TypeKind == TypeKind.Delegate).ToArray();
            if (delegateTypes.Length > 0 && types.Length != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.DelegateWithOtherTypes,
                    invocation.Syntax.GetLocation(),
                    delegateTypes[0].Name));
            }
            else if (delegateTypes.Length == 1 &&
                delegateTypes[0] is INamedTypeSymbol delegateType &&
                NestedTypeStunt.NonPartialContainer(delegateType) is INamedTypeSymbol delegateContainer)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.ContainingTypeNotPartial,
                    invocation.Syntax.GetLocation(),
                    delegateType.Name,
                    delegateContainer.Name));
            }

            var classes = types.Where(x => x.TypeKind == TypeKind.Class).ToArray();
            if (classes.Length > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.DuplicateBaseType,
                    invocation.Syntax.GetLocation()));
            }
            if (classes.Length == 1)
            {
                if (classes[0].IsSealed)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.SealedBaseType,
                        invocation.Syntax.GetLocation(),
                        classes[0].Name));
                }
                else if (types.IndexOf(classes[0]) != 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.BaseTypeNotFirst,
                        invocation.Syntax.GetLocation(),
                        classes[0].Name));
                }
                else if (classes[0] is INamedTypeSymbol named &&
                    NestedTypeStunt.NonPartialContainer(named) is INamedTypeSymbol container)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.ContainingTypeNotPartial,
                        invocation.Syntax.GetLocation(),
                        named.Name,
                        container.Name));
                }
            }
        }
    }
}
