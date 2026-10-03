using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Stunts.CodeAnalysis;
using Stunts.Processors;

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
            new CSharpAot(),
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

        /// <summary>
        /// Default assembly attribute used to explicitly request generated stunts, 
        /// such as <c>[assembly: Stunt&lt;IFoo&gt;]</c>.
        /// </summary>
        public static Type DefaultRegistrationAttribute { get; } = typeof(StuntAttribute<>);

        /// <summary>
        /// The generic assembly attribute whose type arguments request generated stunts. 
        /// Attributes with the same namespace and name and any arity are considered too, 
        /// so the stunt can implement additional interfaces.
        /// </summary>
        public Type RegistrationAttribute { get; init; } = DefaultRegistrationAttribute;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(GeneratorExecutionContext context)
        {
            context.AnalyzerConfigOptions.CheckDebugger(nameof(StuntGenerator));

            if (BuildProperties.StuntsAnalyzerDir(context.AnalyzerConfigOptions.GlobalOptions) is string analyzerDir)
                DependencyResolver.AddSearchPath(analyzerDir);

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

            OnExecute(new ProcessorContext(context) { NamingConvention = NamingConvention }, NamingConvention);
        }

        /// <inheritdoc/>
        public void Initialize(GeneratorInitializationContext context)
            => context.RegisterForSyntaxNotifications(()
                => new AggregateSyntaxReceiver(
                    new ISyntaxReceiver[] { new StuntGeneratorReceiver(RegistrationAttribute) }
                    .Concat(receivers.Select(x => x())).ToArray()));

        /// <summary>
        /// Replaces the <see cref="GeneratorAttribute"/> in use.
        /// </summary>
        public StuntGenerator WithGeneratorAttribute(Type generatorAttribute)
            => this with { GeneratorAttribute = generatorAttribute };

        /// <summary>
        /// Replaces the <see cref="RegistrationAttribute"/> in use.
        /// </summary>
        public StuntGenerator WithRegistrationAttribute(Type registrationAttribute)
            => this with { RegistrationAttribute = registrationAttribute };

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
            var closure = StuntClosure.Create(context.Compilation, GeneratorAttribute)!;
            var assemblyName = context.Compilation.Assembly.Name;
            var stunts = new Dictionary<string, GeneratedStunt>();
            var generated = new List<GeneratedStunt>();
            var defaults = new HashSet<string>();
            var hintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generatorReceiver = context.SyntaxReceivers.OfType<StuntGeneratorReceiver>().FirstOrDefault();

            foreach (var (source, candidate, requester) in context.SyntaxReceivers
                .OfType<IStuntCandidatesReceiver>()
                .SelectMany(receiver => receiver is StuntGeneratorReceiver stuntReceiver
                    ? stuntReceiver.GetStunts(context, closure)
                    : receiver.GetCandidates(context).Select(pair => (pair.source, pair.candidate, assemblyName)))
                .ToArray())
            {
                var inaccessible = candidate.Select(type => (Type: type, Member: InterfaceImplementation.InaccessibleMember(type, context.Compilation)))
                    .FirstOrDefault(pair => pair.Member != null);
                if (inaccessible.Member != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.InaccessibleInterfaceMember, source.GetLocation(),
                        inaccessible.Type.Name, inaccessible.Member.ToDisplayString()));
                    continue;
                }

                var unsupported = candidate.Select(type => (Type: type, Member: RuntimeSignature.UnsupportedMember(type, context.Compilation)))
                    .FirstOrDefault(pair => pair.Member != null);
                if (unsupported.Member != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.UnsupportedRuntimeSignature, source.GetLocation(),
                        unsupported.Type.Name, unsupported.Member.ToDisplayString()));
                    continue;
                }

                if (candidate.FirstOrDefault(type => type.TypeKind == TypeKind.Class && type.IsSealed) is INamedTypeSymbol sealedType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.SealedBaseType, source.GetLocation(), sealedType.Name));
                    continue;
                }

                if (candidate.Any(type => type.TypeKind == TypeKind.Delegate) &&
                    !candidate.TryValidateGeneratorTypes(out _))
                    continue;

                var name = naming.GetNamespace(candidate) + "." + naming.GetName(candidate);
                if (stunts.TryGetValue(name, out var existing))
                {
                    existing.Assemblies.Add(requester);
                    continue;
                }

                if (candidate.FirstOrDefault(type => type.TypeKind != TypeKind.Interface) is INamedTypeSymbol nested &&
                    NestedTypeStunt.NonPartialContainer(nested) != null)
                    continue;

                if (candidate.FirstOrDefault(NestedTypeStunt.MustNest) is INamedTypeSymbol mustNest &&
                    GenericContainer(mustNest) is INamedTypeSymbol genericContainer)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StuntDiagnostics.GenericContainingType, source.GetLocation(), mustNest.Name, genericContainer.Name));
                    continue;
                }

                var syntax = factory.CreateSyntax(naming, candidate);
                var stuntContext = context with { DefaultImplementations = new(SymbolEqualityComparer.Default) };
                var updated = driver.Process(syntax, stuntContext);
                if (syntax.IsEquivalentTo(updated))
                    continue;

                if (BuildProperties.NativeAot(context.AnalyzerConfigOptions.GlobalOptions))
                {
                    if (updated.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method => method.TypeParameterList != null))
                        context.ReportDiagnostic(Diagnostic.Create(StuntDiagnostics.AotUnsupportedMember, source.GetLocation(), "generic intercepted methods require runtime MakeGenericMethod"));
                    if (updated.GetAnnotatedNodes("Stunts.AotQueryable").Any())
                        context.ReportDiagnostic(Diagnostic.Create(StuntDiagnostics.AotUnsupportedMember, source.GetLocation(), "IQueryable default values require dynamic code; register a typed DefaultValueProvider factory"));
                    if (updated.GetAnnotatedNodes("Stunts.AotRefStruct").Any())
                        context.ReportDiagnostic(Diagnostic.Create(StuntDiagnostics.AotUnsupportedMember, source.GetLocation(), "ref-struct interception requires by-ref-like generic runtime support (.NET 9 or later)"));
                }

                // At this point, we should have a type that has at least one public constructor
                if (!updated.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Any(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword)))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            StuntDiagnostics.BaseTypeNoContructor,
                            source.GetLocation(),
                            candidate[0].Name));
                    continue;
                }

                var stunt = new GeneratedStunt(name, candidate, source, updated);
                stunt.Assemblies.Add(requester);
                stunts.Add(name, stunt);
                generated.Add(stunt);

                foreach (var iface in stuntContext.DefaultImplementations)
                {
                    var defaultName = naming.GetNamespace(new[] { iface }) + "." + naming.GetDefaultImplementationName(iface);
                    if (!defaults.Add(defaultName))
                        continue;

                    AddSource(context, defaultName,
                        DefaultImplementation.Driver.Process(DefaultImplementation.CreateSyntax(naming, iface), context), hintNames);
                }
            }

            string? registrations = null;
            if (generated.Count > 0)
            {
                if (((CSharpParseOptions)context.ParseOptions).LanguageVersion < LanguageVersion.CSharp9)
                    context.ReportDiagnostic(Diagnostic.Create(StuntDiagnostics.LanguageVersionNotSupported, generated[0].Source.GetLocation()));
                else
                    registrations = StuntRegistrations.Register(context, naming, generated);
            }

            foreach (var stunt in generated)
                AddSource(context, stunt.Name, stunt.Syntax, hintNames);

            if (registrations != null)
                context.AddSource(UniqueHintName("Stunts.Generated.StuntRegistrations", hintNames), SourceText.From(registrations, Encoding.UTF8));

            if (generatorReceiver != null &&
                StuntRegistrations.Definitions(context, closure, generatorReceiver.GetGeneratorMethods(context, closure)) is string definitions)
                context.AddSource(UniqueHintName("Stunts.Generated.StuntDefinitions", hintNames), SourceText.From(definitions, Encoding.UTF8));
        }

        static INamedTypeSymbol? GenericContainer(INamedTypeSymbol type)
        {
            for (var current = type.ContainingType; current != null; current = current.ContainingType)
            {
                if (current.IsGenericType)
                    return current;
            }

            return null;
        }

        static string UniqueHintName(string name, HashSet<string> hintNames)
        {
            var hintName = name;
            var suffix = 1;
            while (!hintNames.Add(hintName))
                hintName = name + "." + suffix++;

            return hintName;
        }

        static void AddSource(ProcessorContext context, string name, SyntaxNode updated, HashSet<string> hintNames)
        {
            var hintName = UniqueHintName(name, hintNames);
            var code = updated.NormalizeWhitespace().ToFullString();
            var options = context.AnalyzerConfigOptions.GlobalOptions;
            // Pretty-printing is C# only. The debugger dump below still uses the flag for every language.
            var shouldEmit = BuildProperties.EmitCompilerGeneratedFiles(options);
            if (shouldEmit && context.Language == LanguageNames.CSharp)
            {
                updated = CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)context.ParseOptions).GetRoot();
                updated = new CSharpFormatter().Visit(updated);
                code = updated.GetText().ToString();
            }

            context.AddSource(hintName, SourceText.From(code, Encoding.UTF8));

#if DEBUG
            if (Debugger.IsAttached)
            {
                if (shouldEmit &&
                    BuildProperties.IntermediateOutputPath(options) is string intermediateDir &&
                    BuildProperties.MSBuildProjectDirectory(options) is string projectDir)
                {
                    var targetDir = Path.Combine(projectDir, intermediateDir, "generated", nameof(StuntGenerator));
                    Directory.CreateDirectory(targetDir);

                    var filePath = Path.Combine(targetDir, hintName + (context.Language == LanguageNames.CSharp ? ".cs" : ".vb"));
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

        static bool CanGenerateFor(INamedTypeSymbol? symbol) => symbol != null && symbol.TypeKind != TypeKind.Error;

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
        /// A <see cref="ISyntaxReceiver"/> that collects invocations to generator methods, 
        /// generator method declarations, and assembly-level stunt registrations.
        /// </summary>
        class StuntGeneratorReceiver : IStuntCandidatesReceiver
        {
            readonly Type registrationAttribute;
            readonly List<(InvocationExpressionSyntax Invocation, string? Name)> invocations = new();
            readonly List<AttributeSyntax> attributes = new();
            readonly List<SyntaxNode> methods = new();

            public StuntGeneratorReceiver(Type registrationAttribute) => this.registrationAttribute = registrationAttribute;

            public IEnumerable<(SyntaxNode source, INamedTypeSymbol[] candidate)> GetCandidates(ProcessorContext context)
                => StuntClosure.Create(context.Compilation, StuntGenerator.DefaultGeneratorAttribute) is StuntClosure closure
                    ? GetStunts(context, closure).Select(stunt => (stunt.Source, stunt.Types))
                    : Enumerable.Empty<(SyntaxNode, INamedTypeSymbol[])>();

            /// <summary>
            /// Gets the stunts requested by generator method invocations (closing generic wrappers) 
            /// and assembly attributes, with the name of the assembly that requests each at run time.
            /// </summary>
            public IEnumerable<(SyntaxNode Source, INamedTypeSymbol[] Types, string Assembly)> GetStunts(ProcessorContext context, StuntClosure closure)
            {
                var compilation = context.Compilation;
                var models = new Dictionary<SyntaxTree, SemanticModel>();
                SemanticModel Model(SyntaxTree tree)
                {
                    if (!models.TryGetValue(tree, out var model))
                        models.Add(tree, model = compilation.GetSemanticModel(tree));
                    return model;
                }

                // Invocations without explicit type arguments are only bound when they may 
                // be inferred invocations of a known generator method.
                var names = new HashSet<string>(methods.Select(method => method switch
                {
                    MethodDeclarationSyntax declaration => declaration.Identifier.ValueText,
                    LocalFunctionStatementSyntax local => local.Identifier.ValueText,
                    _ => "",
                }));
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                    foreach (var id in StuntClosure.DefinitionMethods(assembly).Select(group => group.Key))
                        names.Add(MethodName(id));

                // Identity forwards are omitted from the definition methods, so their names come from the attribute.
                closure.AddReferencedGeneratorNames(names);

                foreach (var (invocation, name) in invocations)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (name != null && !names.Contains(name))
                        continue;

                    if (Model(invocation.SyntaxTree).GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
                        !method.IsGenericMethod || !closure.IsGenerator(method))
                        continue;

                    foreach (var (types, assembly) in closure.Close(method, context.CancellationToken))
                    {
                        if (Candidate(types) is INamedTypeSymbol[] candidate)
                            yield return (invocation, candidate, assembly);
                    }
                }

                var own = compilation.Assembly.Name;
                foreach (var attribute in attributes)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (Model(attribute.SyntaxTree).GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor &&
                        constructor.ContainingType is { IsGenericType: true } type &&
                        IsRegistrationAttribute(type) &&
                        Candidate(type.TypeArguments) is INamedTypeSymbol[] candidate)
                        yield return (attribute, candidate, own);
                }
            }

            /// <summary>Gets the generator methods declared in source.</summary>
            public IEnumerable<IMethodSymbol> GetGeneratorMethods(ProcessorContext context, StuntClosure closure)
            {
                foreach (var syntax in methods)
                {
                    var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                    if (model.GetDeclaredSymbol(syntax, context.CancellationToken) is IMethodSymbol method && closure.IsGenerator(method))
                        yield return method;
                }
            }

            bool IsRegistrationAttribute(INamedTypeSymbol type)
            {
                var name = registrationAttribute.Name;
                var tick = name.IndexOf('`');
                if (tick > 0)
                    name = name.Substring(0, tick);

                return type.Name == name &&
                    type.ContainingType == null &&
                    type.ContainingNamespace.ToDisplayString() == registrationAttribute.Namespace;
            }

            static INamedTypeSymbol[]? Candidate(ImmutableArray<ITypeSymbol> types)
            {
                if (types.Length == 0 || !types.All(type => type is INamedTypeSymbol named && CanGenerateFor(named)))
                    return null;

                return types.Select(type => (INamedTypeSymbol)type.WithNullableAnnotation(NullableAnnotation.None)).ToArray();
            }

            static string MethodName(string id)
            {
                var end = id.IndexOf('(');
                if (end < 0)
                    end = id.Length;
                var arity = id.LastIndexOf("``", end, StringComparison.Ordinal);
                if (arity > 0)
                    end = arity;
                var start = id.LastIndexOf('.', end - 1) + 1;
                return id.Substring(start, end - start);
            }

            public void OnVisitSyntaxNode(SyntaxNode node)
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        // Class.Method<T, ...>() and Method<T, ...>() are always considered, 
                        // Class.Method(...) and Method(...) only if the name is a generator's.
                        var name = invocation.Expression switch
                        {
                            MemberAccessExpressionSyntax { Name: GenericNameSyntax } => null,
                            GenericNameSyntax => null,
                            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
                            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                            _ => "",
                        };
                        if (name != "")
                            invocations.Add((invocation, name));
                        break;
                    case AttributeSyntax attribute when attribute.Parent is AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword } &&
                        attribute.Name.DescendantNodesAndSelf().OfType<GenericNameSyntax>().Any():
                        attributes.Add(attribute);
                        break;
                    case MethodDeclarationSyntax method when method.TypeParameterList != null && method.AttributeLists.Count > 0:
                        methods.Add(method);
                        break;
                    case LocalFunctionStatementSyntax local when local.TypeParameterList != null && local.AttributeLists.Count > 0:
                        methods.Add(local);
                        break;
                }
            }
        }
    }
}