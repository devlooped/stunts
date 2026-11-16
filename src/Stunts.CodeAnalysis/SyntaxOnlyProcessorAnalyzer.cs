using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Reports code in a <c>[SyntaxOnlyProcessor]</c> type that reads inputs the stunt
    /// generation cache does not track. The cache reuses the output of those processors
    /// from other assemblies while the syntax they receive is unchanged, so anything else
    /// they read would replay stale code.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class SyntaxOnlyProcessorAnalyzer : DiagnosticAnalyzer
    {
        const string AttributeName = "Stunts.SyntaxOnlyProcessorAttribute";
        const string ContextName = "Stunts.ProcessorContext";

        // Context members the fingerprint describes (the naming convention's type and settings)
        // or that do not change the output.
        static readonly HashSet<string> ContextMembers = new()
        {
            "Language",
            "CancellationToken",
            "NamingConvention",
        };

        /// <inheritdoc/>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(StuntDiagnostics.SyntaxOnlyProcessorUsesSemantics);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(start =>
            {
                if (start.Compilation.GetTypeByMetadataName(AttributeName) is not INamedTypeSymbol attribute ||
                    start.Compilation.GetTypeByMetadataName(ContextName) is not INamedTypeSymbol processorContext)
                    return;

                var analysis = new Analysis(start.Compilation, attribute, processorContext);
                start.RegisterOperationAction(analysis.Analyze,
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference,
                    OperationKind.FieldReference,
                    OperationKind.EventReference,
                    OperationKind.MethodReference,
                    OperationKind.ParameterReference);
            });
        }

        sealed class Analysis
        {
            readonly INamedTypeSymbol attribute;
            readonly INamedTypeSymbol processorContext;
            readonly ISymbol?[] semanticBases;
            readonly ISymbol?[] semanticInterfaces;
            readonly ConcurrentDictionary<INamedTypeSymbol, INamedTypeSymbol?> processors = new(SymbolEqualityComparer.Default);

            public Analysis(Compilation compilation, INamedTypeSymbol attribute, INamedTypeSymbol processorContext)
            {
                this.attribute = attribute;
                this.processorContext = processorContext;
                semanticBases = new[]
                {
                    compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.Compilation"),
                    compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.SemanticModel"),
                };
                semanticInterfaces = new[]
                {
                    compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.ISymbol"),
                    compilation.GetTypeByMetadataName("Microsoft.CodeAnalysis.IOperation"),
                };
            }

            public void Analyze(OperationAnalysisContext context)
            {
                var containing = context.ContainingSymbol as INamedTypeSymbol ?? context.ContainingSymbol.ContainingType;
                if (containing == null || GetProcessor(containing) is not INamedTypeSymbol processor ||
                    IsInNameOf(context.Operation))
                    return;

                if (context.Operation is IParameterReferenceOperation parameter)
                {
                    if (IsSemantic(parameter.Parameter.Type))
                        Report(context, processor, $"reads '{parameter.Parameter.Name}', which the stunt generation cache does not track");
                    return;
                }

                var (member, arguments) = GetMember(context.Operation);
                if (member == null)
                    return;

                string? usage = null;
                if (SymbolEqualityComparer.Default.Equals(member.ContainingType?.OriginalDefinition, processorContext))
                {
                    if (!ContextMembers.Contains(member.Name))
                        usage = $"reads '{member.ContainingType!.Name}.{member.Name}', which the stunt generation cache does not track";
                }
                // Members of a semantic value were reported where the value was obtained.
                else if (IsSemantic(member) && !IsSemantic(GetReceiver(context.Operation)?.Type))
                {
                    usage = $"reads '{member.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}', which the stunt generation cache does not track";
                }
                else if (member.ContainingType is INamedTypeSymbol target && GetProcessor(target) == null &&
                    arguments.Any(argument => IsContext(argument.Value.Type)))
                {
                    // The analysis cannot follow the context into code outside syntax-only processors.
                    usage = $"passes '{processorContext.Name}' to '{member.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}', which is not part of a syntax-only processor";
                }

                if (usage != null)
                    Report(context, processor, usage);
            }

            static void Report(OperationAnalysisContext context, INamedTypeSymbol processor, string usage)
                => context.ReportDiagnostic(Diagnostic.Create(
                    StuntDiagnostics.SyntaxOnlyProcessorUsesSemantics,
                    context.Operation.Syntax.GetLocation(),
                    processor.Name,
                    usage));

            // The value a member is accessed on, including the first argument of an extension method.
            static IOperation? GetReceiver(IOperation operation) => operation switch
            {
                IInvocationOperation invocation when invocation.Instance != null => invocation.Instance,
                IInvocationOperation invocation when invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0
                    => invocation.Arguments[0].Value,
                IMemberReferenceOperation reference => reference.Instance,
                _ => null,
            };

            static (ISymbol? Member, ImmutableArray<IArgumentOperation> Arguments) GetMember(IOperation operation) => operation switch
            {
                IInvocationOperation invocation => (invocation.TargetMethod, invocation.Arguments),
                IObjectCreationOperation creation => (creation.Constructor, creation.Arguments),
                IPropertyReferenceOperation property => (property.Property, property.Arguments),
                IFieldReferenceOperation field => (field.Field, ImmutableArray<IArgumentOperation>.Empty),
                IEventReferenceOperation @event => (@event.Event, ImmutableArray<IArgumentOperation>.Empty),
                IMethodReferenceOperation method => (method.Method, ImmutableArray<IArgumentOperation>.Empty),
                _ => (null, ImmutableArray<IArgumentOperation>.Empty),
            };

            // The syntax-only processor declaring the type, which can be nested in it
            // (e.g. a syntax rewriter).
            INamedTypeSymbol? GetProcessor(INamedTypeSymbol type) => processors.GetOrAdd(type, type =>
            {
                for (var current = type.OriginalDefinition; current != null; current = current.ContainingType)
                {
                    if (current.GetAttributes().Any(data => SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)))
                        return current;
                }

                return null;
            });

            bool IsSemantic(ISymbol member) => member switch
            {
                IMethodSymbol method => IsSemantic(method.ContainingType) ||
                    (method.MethodKind != MethodKind.Constructor && IsSemantic(method.ReturnType)) ||
                    method.Parameters.Any(parameter => IsSemantic(parameter.Type)),
                IPropertySymbol property => IsSemantic(property.ContainingType) || IsSemantic(property.Type) ||
                    property.Parameters.Any(parameter => IsSemantic(parameter.Type)),
                IFieldSymbol field => IsSemantic(field.ContainingType) || IsSemantic(field.Type),
                IEventSymbol @event => IsSemantic(@event.ContainingType),
                _ => false,
            };

            bool IsSemantic(ITypeSymbol? type)
            {
                while (type is IArrayTypeSymbol array)
                    type = array.ElementType;

                if (type == null)
                    return false;

                for (var current = type; current != null; current = current.BaseType)
                {
                    if (semanticBases.Contains(current.OriginalDefinition, SymbolEqualityComparer.Default))
                        return true;
                }

                return semanticInterfaces.Contains(type.OriginalDefinition, SymbolEqualityComparer.Default) ||
                    type.AllInterfaces.Any(@interface => semanticInterfaces.Contains(@interface.OriginalDefinition, SymbolEqualityComparer.Default));
            }

            bool IsContext(ITypeSymbol? type)
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, processorContext))
                        return true;
                }

                return false;
            }

            static bool IsInNameOf(IOperation operation)
            {
                for (var current = operation.Parent; current != null; current = current.Parent)
                {
                    if (current is INameOfOperation)
                        return true;
                }

                return false;
            }
        }
    }
}
