using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Stunts.CodeAnalysis;
using Stunts.Processors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Stunts
{
    /// <summary>
    /// Main source generator for stunts. Can also be consumed from within other source 
    /// generators that wish to customize the process for their own flavors of stunts.
    /// </summary>
    [Generator]
    public record StuntGenerator : ISourceGenerator
    {
        ImmutableArray<Func<ISyntaxReceiver>> receivers = ImmutableArray<Func<ISyntaxReceiver>>.Empty;

        /// <summary>
        /// Default naming convention used when generating documents, unless overridden
        /// via the corresponding constructor argument.
        /// </summary>
        public static NamingConvention DefaultNamingConvention { get; } = new NamingConvention();

        /// <summary>
        /// Default method attribute used to flag a generic method as stunt-generating,
        /// meaning invocations to that method are used to trigger source generation.
        /// </summary>
        public static Type DefaultGeneratorAttribute { get; } = typeof(StuntGeneratorAttribute);

        /// <summary>
        /// Instantiates the set of default <see cref="ISyntaxProcessor"/> for the generator.
        /// </summary>
        public static ISyntaxProcessor[] DefaultProcessors => new ISyntaxProcessor[]
        {
            new DefaultImports(),
            new CSharpRewrite(),
            new CSharpStunt(),
            new CSharpGenerated(),
            new FixupImports(),
            new CSharpFileHeader(),
            new CSharpPragmas(),
        };

        /// <summary>
        /// Creates a default stunt generator, using <see cref="DefaultNamingConvention"/>, 
        /// <see cref="DefaultGeneratorAttribute"/> and <see cref="DefaultProcessors"/>.
        /// </summary>
        public StuntGenerator() : this(DefaultNamingConvention, DefaultGeneratorAttribute, DefaultProcessors) { }

        /// <summary>
        /// Creates a new instance of the <see cref="StuntGenerator"/>.
        /// </summary>
        /// <param name="naming">The naming convention to apply to generated code.</param>
        /// <param name="generatorAttribute">The attribute used to flag generic methods that 
        /// should trigger stunt generation. The generic type parameters passed to invocations 
        /// of those methods are used when invoking <see cref="ISyntaxProcessor.Process"/> in 
        /// the <see cref="ProcessorContext.TypeArguments"/>.
        /// </param>
        /// <param name="processors">Processors to use during source generation.</param>
        public StuntGenerator(NamingConvention naming, Type generatorAttribute, params ISyntaxProcessor[] processors)
            => (NamingConvention, GeneratorAttribute, Processors)
            = (naming, generatorAttribute, processors.ToImmutableArray());

        /// <summary>
        /// Creates a new instance of the <see cref="StuntGenerator"/>.
        /// </summary>
        /// <param name="naming">The naming convention to apply to generated code.</param>
        /// <param name="generatorAttribute">The attribute used to flag generic methods that 
        /// should trigger stunt generation. The generic type parameters passed to invocations 
        /// of those methods are used when invoking <see cref="ISyntaxProcessor.Process"/> in 
        /// the <see cref="ProcessorContext.TypeArguments"/>.
        /// </param>
        /// <param name="processors">Processors to use during source generation.</param>
        public StuntGenerator(NamingConvention naming, Type generatorAttribute, IEnumerable<ISyntaxProcessor> processors)
            => (NamingConvention, GeneratorAttribute, Processors)
            = (naming, generatorAttribute, processors.ToImmutableArray());

        /// <summary>
        /// Naming convention used for generated types.
        /// </summary>
        public NamingConvention NamingConvention { get; init; }

        /// <summary>
        /// The attribute used to flag generic methods that 
        /// should trigger stunt generation.
        /// </summary>
        public Type GeneratorAttribute { get; init; }

        /// <summary>
        /// Registered <see cref="ISyntaxProcessor"/> that are applied when the generator 
        /// executes.
        /// </summary>
        public ImmutableArray<ISyntaxProcessor> Processors { get; init; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(GeneratorExecutionContext context)
        {
            context.AnalyzerConfigOptions.CheckDebugger(nameof(StuntGenerator));

            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.StuntsAnalyzerDir", out var analyerDir))
                DependencyResolver.AddSearchPath(analyerDir);

            var generatorAttr = context.Compilation.GetTypeByMetadataName(GeneratorAttribute.FullName);
            if (generatorAttr == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        StuntDiagnostics.GeneratorAttributeNotFound,
                        null,
                        GeneratorAttribute.FullName));
                return;
            }

            OnExecute(new ProcessorContext(context), NamingConvention);
        }

        /// <inheritdoc/>
        public void Initialize(GeneratorInitializationContext context)
            => context.RegisterForSyntaxNotifications(()
                => new AggregateSyntaxReceiver(
                    new ISyntaxReceiver[] { new StuntGeneratorReceiver(GeneratorAttribute) }
                    .Concat(receivers.Select(x => x())).ToArray()));

        /// <summary>
        /// Replaces the <see cref="GeneratorAttribute"/> in use.
        /// </summary>
        public StuntGenerator WithGeneratorAttribute(Type generatorAttribute)
            => this with { GeneratorAttribute = generatorAttribute };

        /// <summary>
        /// Replaces the <see cref="NamingConvention"/> in use.
        /// </summary>
        public StuntGenerator WithNamingConvention(NamingConvention naming)
            => this with { NamingConvention = naming };

        /// <summary>
        /// Registers an additional <see cref="ISyntaxProcessor"/> to use during the generation phase.
        /// </summary>
        public StuntGenerator WithProcessor(ISyntaxProcessor processor)
            => this with { Processors = Processors.Add(processor) };

        /// <summary>
        /// Replaces all previously registered processors with the given <paramref name="processors"/>.
        /// </summary>
        public StuntGenerator WithProcessors(params ISyntaxProcessor[] processors)
            => this with { Processors = processors.ToImmutableArray() };

        /// <summary>
        /// Registers an additional syntax receiver factory that can collect syntax nodes for the generation 
        /// phase. 
        /// </summary>
        /// <remarks>
        /// The instance of the registered receiver can later be retrieved from an <see cref="ISyntaxProcessor"/> 
        /// via the <see cref="ProcessorContext.SyntaxReceivers"/> like:
        /// <code>
        /// var receiver = context.SyntaxReceivers.OfType{MyReceiver}();
        /// </code>
        /// </remarks>
        public StuntGenerator WithSyntaxReceiver(Func<ISyntaxReceiver> receiverFactory)
            => this with { receivers = receivers.Add(receiverFactory) };

        void OnExecute(ProcessorContext context, NamingConvention naming)
        {
            var processors = Processors;
            if (!processors.Any(x => x.Phase == ProcessorPhase.Scaffold))
                processors = processors.Add(new MemberScaffold());

            var driver = new SyntaxProcessorDriver(processors);
            var factory = StuntSyntaxFactory.CreateFactory(context.Language);
            var stunts = new HashSet<string>();

            foreach (var (source, candidate) in context.SyntaxReceivers
                .OfType<IStuntCandidatesReceiver>()
                .SelectMany(receiver => receiver.GetCandidates(context)).ToArray())
            {
                var name = naming.GetName(candidate);
                if (stunts.Contains(name))
                    continue;

                var syntax = factory.CreateSyntax(naming, candidate);
                var updated = driver.Process(syntax, context);
                if (syntax.IsEquivalentTo(updated))
                    continue;

                // At this point, we should have a type that has at least one public constructor
                if (!updated.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Any())
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            StuntDiagnostics.BaseTypeNoContructor,
                            source.GetLocation(),
                            candidate[0].Name));
                    continue;
                }

                var code = updated.NormalizeWhitespace().ToFullString();
                var shouldEmit = false;

                // Additional pretty-printing when emitting generated files, improves whitespace handling for C#
                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.EmitCompilerGeneratedFiles", out var emitSources) &&
                    bool.TryParse(emitSources, out shouldEmit) &&
                    shouldEmit &&
                    // NOTE: checking for C# last, since the Debugger.Attached section below would depend on 
                    // the proper initialization of shouldEmit too, regardless of language
                    context.Language == LanguageNames.CSharp)
                {
                    updated = CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)context.ParseOptions).GetRoot();
                    updated = new CSharpFormatter().Visit(updated);
                    code = updated.GetText().ToString();
                }

                stunts.Add(name);
                context.AddSource(name, SourceText.From(code, Encoding.UTF8));

#if DEBUG
                if (Debugger.IsAttached)
                {
                    if (shouldEmit &&
                        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IntermediateOutputPath", out var intermediateDir) &&
                        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDir))
                    {
                        var targetDir = Path.Combine(projectDir, intermediateDir, "generated", nameof(StuntGenerator));
                        Directory.CreateDirectory(targetDir);

                        var filePath = Path.Combine(targetDir, name + (context.Language == LanguageNames.CSharp ? ".cs" : ".vb"));
                        File.WriteAllText(filePath, code);
                        Debugger.Log(0, "", "Stunt Generated: " + filePath + Environment.NewLine);
                    }

                    Debugger.Log(0, "", string.Join(
                            Environment.NewLine,
                            code.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                .Select((line, index) => index.ToString().PadLeft(3) + " " + line)) + Environment.NewLine);
                }
#endif
            }
        }

        class CSharpFormatter : CSharpSyntaxRewriter
        {
            static SyntaxTrivia NewLine => SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\n");
            static SyntaxTrivia Tab => SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "    ");

            public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
                // Ctor is always replaced, so it needs whitespace
                => base.VisitConstructorDeclaration(node)!
                    .WithTrailingTrivia(node.GetTrailingTrivia().Add(NewLine));

            public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                var canRead = node.AccessorList?.Accessors.Any(SyntaxKind.GetAccessorDeclaration) == true;
                var canWrite = node.AccessorList?.Accessors.Any(SyntaxKind.SetAccessorDeclaration) == true;

                // If there's get+set, the property declaration is preserved from scaffold
                if (canRead && canWrite)
                    return base.VisitPropertyDeclaration(node);

                return base.VisitPropertyDeclaration(node)!
                    .WithTrailingTrivia(node.GetTrailingTrivia().Add(NewLine));
            }

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                // ref/out already get proper whitespace from scaffold
                if (!node.ParameterList.Parameters.Any(x => x.IsRefOut()))
                    return base.VisitMethodDeclaration(node)!
                        .WithTrailingTrivia(node.GetTrailingTrivia().Add(NewLine));

                return base.VisitMethodDeclaration(node);
            }

            public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
                => base.VisitFieldDeclaration(node)!
                    .WithTrailingTrivia(node.GetTrailingTrivia().Add(NewLine));

            SyntaxTriviaList? indent;

            public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                indent = node.Initializer?.GetLeadingTrivia();

                return base.VisitObjectCreationExpression(node);
            }

            public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
            {
                if (node.Kind() == SyntaxKind.ComplexElementInitializerExpression)
                    return base.VisitInitializerExpression(node)!.WithLeadingTrivia(indent?.Insert(0, NewLine).Add(Tab));

                if (node.Kind() == SyntaxKind.CollectionInitializerExpression)
                {
                    var last = node.Expressions.Count - 1;
                    return base.VisitInitializerExpression(node
                        .WithExpressions(SyntaxFactory.SeparatedList(
                            node.Expressions.Select((e, i) => i != last ? e : e.WithTrailingTrivia(indent?.Insert(0, NewLine))))));
                }

                return base.VisitInitializerExpression(node);
            }
        }

        static bool CanGenerateFor(INamedTypeSymbol? symbol)
        {
            if (symbol == null)
                return false;

            // Cannot generate for types using pointer types
            var usesPointers = symbol.GetMembers()
                .OfType<IMethodSymbol>()
                .SelectMany(method => method.Parameters)
                .Any(parameter => parameter.Type.Kind == SymbolKind.PointerType);

            return !usesPointers;
        }

        class AggregateSyntaxReceiver : ISyntaxReceiver, IEnumerable
        {
            readonly ISyntaxReceiver[] receivers;

            public AggregateSyntaxReceiver(ISyntaxReceiver[] receivers)
                => this.receivers = receivers;

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                foreach (var receiver in receivers)
                    receiver.OnVisitSyntaxNode(syntaxNode);
            }

            IEnumerator IEnumerable.GetEnumerator() => receivers.GetEnumerator();
        }

        /// <summary>
        /// A <see cref="ISyntaxReceiver"/> that collects invocations to generic methods, 
        /// which are initial candidates for lookup.
        /// </summary>
        class StuntGeneratorReceiver : IStuntCandidatesReceiver
        {
            readonly Type generatorAttribute;
            readonly List<(InvocationExpressionSyntax, GenericNameSyntax)> invocations = new();

            public StuntGeneratorReceiver(Type generatorAttribute) => this.generatorAttribute = generatorAttribute;

            public IEnumerable<(SyntaxNode source, INamedTypeSymbol[] candidate)> GetCandidates(ProcessorContext context)
            {
                var generatorAttr = context.Compilation.GetTypeByMetadataName(generatorAttribute.FullName);
                if (generatorAttr == null)
                    yield break;

                foreach (var (invocation, genericName) in invocations)
                {
                    var semantic = context.Compilation.GetSemanticModel(invocation.SyntaxTree);
                    var symbol = semantic.GetSymbolInfo(invocation, context.CancellationToken);
                    if (symbol.Symbol is not IMethodSymbol method)
                        continue;

                    if (!method.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generatorAttr)))
                        continue;

                    var typeArgs = genericName.TypeArgumentList.Arguments
                        .Select(name => semantic.GetSymbolInfo(name, context.CancellationToken).Symbol as INamedTypeSymbol)
                        .ToList();

                    // A corresponding diagnostics analyzer would flag this as a compile error.
                    if (!typeArgs.All(CanGenerateFor))
                        continue;

                    yield return (invocation, typeArgs.Cast<INamedTypeSymbol>().ToArray());
                }
            }

            public void OnVisitSyntaxNode(SyntaxNode node)
            {
                // TODO: VB in the future?
                if (node.IsKind(SyntaxKind.InvocationExpression) &&
                    node is InvocationExpressionSyntax invocation)
                {
                    // Both Class.Method<T, ...>()
                    if (invocation.Expression is MemberAccessExpressionSyntax member &&
                        member.Name is GenericNameSyntax memberName)
                        invocations.Add((invocation, memberName));
                    // And Method<T, ...>()
                    else if (invocation.Expression is GenericNameSyntax methodName)
                        invocations.Add((invocation, methodName));
                }
            }
        }
    }
}
