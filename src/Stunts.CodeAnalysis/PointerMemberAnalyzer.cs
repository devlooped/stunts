using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Stunts.CodeAnalysis;

namespace Stunts
{
    /// <summary>
    /// Previously reported <see cref="StuntDiagnostics.PointerMember"/> for pointer signatures.
    /// Those signatures are now generated, so the analyzer no longer reports.
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
            => _ = context.Compilation.GetTypeByMetadataName(generatorAttribute.FullName);
    }
}
