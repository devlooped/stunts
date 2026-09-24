using System;
using System.Collections.Immutable;
using System.Linq;
using Stunts.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts
{
    /// <summary>
    /// Analyzes source code looking for method invocations to methods annotated with 
    /// the <see cref="StuntGeneratorAttribute"/> and reports unsupported pointer types 
    /// in method arguments for codegen.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class PointerMemberAnalyzer : DiagnosticAnalyzer
    {
        Type generatorAttribute;

        /// <summary>
        /// Instantiates the analyzer for method invocations annotated 
        /// with <see cref="StuntGeneratorAttribute"/>.
        /// </summary>
        public PointerMemberAnalyzer() : this(typeof(StuntGeneratorAttribute)) { }

        /// <summary>
        /// Customizes the analyzer by specifying a custom 
        /// <see cref="generatorAttribute"/> to lookup in method invocations.
        /// </summary>
        protected PointerMemberAnalyzer(Type generatorAttribute)
            => this.generatorAttribute = generatorAttribute;

        /// <summary>
        /// Returns the single descriptor this analyzer supports.
        /// </summary>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(StuntDiagnostics.PointerMember);

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

            if (invocation.TargetMethod.GetAttributes().Any(x =>
                SymbolEqualityComparer.Default.Equals(x.AttributeClass, generator)))
            {
                var args = invocation.TargetMethod.TypeArguments
                    .OfType<INamedTypeSymbol>()
                    .Where(t => t.GetMembers()
                    .OfType<IMethodSymbol>()
                    .SelectMany(method => method.Parameters)
                    .Any(parameter => parameter.Type.Kind == SymbolKind.PointerType))
                    .ToArray();

                if (args.Length > 0)
                {
                    var diagnostic = Diagnostic.Create(
                        StuntDiagnostics.PointerMember,
                        invocation.Syntax.GetLocation(),
                        string.Join(", ", args.Select(t => t.Name)));

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
