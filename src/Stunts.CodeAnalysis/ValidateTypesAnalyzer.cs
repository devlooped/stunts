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
                StuntDiagnostics.EnumType);

        /// <summary>
        /// Registers the analyzer to take action on method invocation expressions.
        /// </summary>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
        }

        void AnalyzeOperation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;

            // Get the matching symbol for the given generator attribute from the current compilation.
            var generator = context.Compilation.GetTypeByMetadataName(generatorAttribute.FullName);
            if (generator == null)
                return;

            if (invocation.TargetMethod.GetAttributes().Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, generator)))
            {
                foreach (var enumType in invocation.TargetMethod.TypeArguments.Where(x => x.TypeKind == TypeKind.Enum))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.EnumType,
                        invocation.Syntax.GetLocation(),
                        enumType.Name));
                }

                var classes = invocation.TargetMethod.TypeArguments.Where(x => x.TypeKind == TypeKind.Class).ToArray();
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
                    else if (invocation.TargetMethod.TypeArguments.IndexOf(classes[0]) != 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            StuntDiagnostics.BaseTypeNotFirst,
                            invocation.Syntax.GetLocation(),
                            classes[0].Name));
                    }
                }
            }
        }
    }
}
