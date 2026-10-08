using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            var profile = GenProfile.Start();
            var processors = Processors;
            if (!processors.Any(x => x.Phase == ProcessorPhase.Scaffold))
                processors = processors.Add(new MemberScaffold());

            var driver = new SyntaxProcessorDriver(processors);
            var factory = StuntSyntaxFactory.CreateFactory(context.Language);
            var closure = StuntClosure.Create(context.Compilation, GeneratorAttribute)!;
            var assemblyName = context.Compilation.Assembly.Name;
            var generated = new List<GeneratedStunt>();
            var defaults = new HashSet<string>();
            var hintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generatorReceiver = context.SyntaxReceivers.OfType<StuntGeneratorReceiver>().FirstOrDefault();

            profile.Mark();
            var candidates = context.SyntaxReceivers
                .OfType<IStuntCandidatesReceiver>()
                .SelectMany(receiver => receiver is StuntGeneratorReceiver stuntReceiver
                    ? stuntReceiver.GetStunts(context, closure)
                    : receiver.GetCandidates(context).Select(pair => (pair.source, pair.candidate, assemblyName)))
                .ToArray();
            profile.Add(ref profile.Discover);

            var pending = new List<PendingStunt>();
            var pendingByName = new Dictionary<string, PendingStunt>();
            var metadataBases = 0;
            foreach (var (source, candidate, requester) in candidates)
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
                if (pendingByName.TryGetValue(name, out var existing))
                {
                    existing.Requesters.Add(requester);
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

                if (MetadataTypes(candidate))
                    metadataBases++;

                var item = new PendingStunt(source, candidate, name, requester);
                pendingByName.Add(name, item);
                pending.Add(item);
            }

            profile.Metadata = metadataBases;
            // One shared, treeless compilation. Each stunt adds only its own generated tree.
            var scaffold = metadataBases > 0 ? context.Compilation.RemoveAllSyntaxTrees() : null;
            profile.Mark();
            Parallel.For(0, pending.Count, new ParallelOptions { CancellationToken = context.CancellationToken }, index =>
            {
                var item = pending[index];
                var started = GenProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
                var syntax = factory.CreateSyntax(naming, item.Candidate);
                if (GenProfile.Enabled)
                    GenProfile.Step("CreateSyntax", Stopwatch.GetTimestamp() - started);

                var stuntContext = context with { DefaultImplementations = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) };
                if (scaffold != null && MetadataTypes(item.Candidate))
                    stuntContext = stuntContext with { MetadataScaffold = scaffold };

                item.Syntax = syntax;
                item.Updated = driver.Process(syntax, stuntContext);
                item.Defaults = stuntContext.DefaultImplementations;
            });
            profile.Add(ref profile.Process);

            foreach (var item in pending)
            {
                var source = item.Source;
                var candidate = item.Candidate;
                var updated = item.Updated;
                if (item.Syntax.IsEquivalentTo(updated))
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

                var stunt = new GeneratedStunt(item.Name, candidate, source, updated);
                foreach (var requester in item.Requesters)
                    stunt.Assemblies.Add(requester);
                generated.Add(stunt);

                foreach (var iface in item.Defaults)
                {
                    var defaultName = naming.GetNamespace(new[] { iface }) + "." + naming.GetDefaultImplementationName(iface);
                    if (!defaults.Add(defaultName))
                        continue;

                    AddSource(context, defaultName,
                        DefaultImplementation.Driver.Process(DefaultImplementation.CreateSyntax(naming, iface), context), hintNames);
                }
            }

            profile.Mark();
            string? registrations = null;
            if (generated.Count > 0)
            {
                if (((CSharpParseOptions)context.ParseOptions).LanguageVersion < LanguageVersion.CSharp9)
                    context.ReportDiagnostic(Diagnostic.Create(StuntDiagnostics.LanguageVersionNotSupported, generated[0].Source.GetLocation()));
                else
                    registrations = StuntRegistrations.Register(context, naming, generated);
            }
            profile.Add(ref profile.Register);

            profile.Mark();
            var emitted = new string[generated.Count];
            Parallel.For(0, generated.Count, new ParallelOptions { CancellationToken = context.CancellationToken }, index =>
                emitted[index] = SyntaxText.Emit(generated[index].Syntax));
            profile.Add(ref profile.Normalize);

            for (var index = 0; index < generated.Count; index++)
                AddSource(context, generated[index].Name, generated[index].Syntax, hintNames, profile, emitted[index]);

            if (registrations != null)
                context.AddSource(UniqueHintName("Stunts.Generated.StuntRegistrations", hintNames), SourceText.From(registrations, Encoding.UTF8));

            if (generatorReceiver != null &&
                StuntRegistrations.Definitions(context, closure, generatorReceiver.GetGeneratorMethods(context, closure)) is string definitions)
                context.AddSource(UniqueHintName("Stunts.Generated.StuntDefinitions", hintNames), SourceText.From(definitions, Encoding.UTF8));

            profile.Write(generated.Count);
        }

        // Source types have to be bound with the user compilation. Metadata types
        // bind from references alone, so the generated tree can stay on a treeless compilation.
        static bool MetadataTypes(INamedTypeSymbol[] types)
        {
            foreach (var type in types)
            {
                if (!MetadataType(type))
                    return false;
            }

            return true;
        }

        static bool MetadataType(ITypeSymbol type)
        {
            if (type.TypeKind is TypeKind.Error or TypeKind.TypeParameter or TypeKind.Dynamic)
                return false;
            if (type is IArrayTypeSymbol array)
                return MetadataType(array.ElementType);
            if (type is not INamedTypeSymbol named)
                return true;
            if (named.DeclaringSyntaxReferences.Length > 0)
                return false;

            foreach (var argument in named.TypeArguments)
            {
                if (!MetadataType(argument))
                    return false;
            }

            return true;
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

        static void AddSource(ProcessorContext context, string name, SyntaxNode updated, HashSet<string> hintNames, GenProfile? profile = null, string? code = null)
        {
            var hintName = UniqueHintName(name, hintNames);
            if (code == null)
            {
                profile?.Mark();
                code = SyntaxText.Emit(updated);
                profile?.Add(ref profile.Normalize);
            }
            var options = context.AnalyzerConfigOptions.GlobalOptions;
            // Pretty-printing is C# only. The debugger dump below still uses the flag for every language.
            var shouldEmit = BuildProperties.EmitCompilerGeneratedFiles(options);
            if (shouldEmit && context.Language == LanguageNames.CSharp)
            {
                profile?.Mark();
                updated = CSharpSyntaxTree.ParseText(code, (CSharpParseOptions)context.ParseOptions).GetRoot();
                updated = new CSharpFormatter().Visit(updated);
                code = updated.GetText().ToString();
                profile?.Add(ref profile.Pretty);
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

                // Every collected invocation carries its method name. Only names of known
                // generator methods are bound, whether or not the call has explicit type arguments.
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
                        // Class.Method<T>() and Method<T>() keep the method name, same as calls
                        // without explicit type arguments. GetStunts binds the call only when
                        // that name belongs to a generator method.
                        var name = invocation.Expression switch
                        {
                            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
                            GenericNameSyntax generic => generic.Identifier.ValueText,
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

    sealed class PendingStunt
    {
        public PendingStunt(SyntaxNode source, INamedTypeSymbol[] candidate, string name, string requester)
        {
            Source = source;
            Candidate = candidate;
            Name = name;
            Requesters = new List<string> { requester };
        }

        public SyntaxNode Source { get; }

        public INamedTypeSymbol[] Candidate { get; }

        public string Name { get; }

        public List<string> Requesters { get; }

        public SyntaxNode Syntax { get; set; } = null!;

        public SyntaxNode Updated { get; set; } = null!;

        public HashSet<INamedTypeSymbol> Defaults { get; set; } = null!;
    }

    sealed class GenProfile
    {
        public static bool Enabled { get; private set; }

        static readonly ConcurrentDictionary<string, StrongBox<long>> steps = new();

        public long Discover;
        public long Process;
        public long Normalize;
        public long Pretty;
        public long Register;
        public int Metadata;
        readonly string? path;
        readonly Stopwatch clock = new Stopwatch();
        long mark;

        GenProfile(string? path) => this.path = path;

        public static GenProfile Start()
        {
            var profile = new GenProfile(Environment.GetEnvironmentVariable("STUNTS_PROFILE"));
            Enabled = profile.path != null;
            if (Enabled)
            {
                steps.Clear();
                profile.clock.Start();
                profile.mark = profile.clock.ElapsedTicks;
            }

            return profile;
        }

        public static void Step(string name, long ticks)
        {
            if (!Enabled || ticks <= 0)
                return;

            var box = steps.GetOrAdd(name, static _ => new StrongBox<long>());
            Interlocked.Add(ref box.Value, ticks);
        }

        public void Mark()
        {
            if (path != null)
                mark = clock.ElapsedTicks;
        }

        public void Add(ref long bucket)
        {
            if (path == null)
                return;

            var now = clock.ElapsedTicks;
            bucket += now - mark;
            mark = now;
        }

        public void Write(int generated)
        {
            if (path == null)
                return;

            var total = clock.ElapsedTicks;
            var lines = new List<string>
            {
                "generated=" + generated,
                "metadata=" + Metadata,
                "discover=" + Seconds(Discover),
                "process=" + Seconds(Process),
                "normalize=" + Seconds(Normalize),
                "pretty=" + Seconds(Pretty),
                "register=" + Seconds(Register),
                "other=" + Seconds(total - Discover - Process - Normalize - Pretty - Register),
                "total=" + Seconds(total),
            };
            foreach (var step in steps.OrderByDescending(pair => pair.Value.Value))
                lines.Add("cpu." + step.Key + "=" + Seconds(step.Value.Value));
            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }

        static string Seconds(long ticks) => (ticks / (double)Stopwatch.Frequency).ToString("0.000");
    }

    /// <summary>
    /// Writes a syntax tree without <see cref="SyntaxNode.NormalizeWhitespace"/>.
    /// That walk rebuilds every token's trivia and was about a third of generation
    /// for thousands of stunts. Line breaks and indentation are trivia attached by
    /// <see cref="ExplicitWhitespace"/>; this only inserts spaces where tokens would
    /// otherwise join.
    /// </summary>
    static class SyntaxText
    {
        public static string Emit(SyntaxNode node)
        {
            node = ExplicitWhitespace.Apply(node);
            var builder = new StringBuilder();
            if (!HasGeneratedMarker(node))
                builder.AppendLine("// <auto-generated/>");
            WriteTokens(builder, node);
            return builder.ToString();
        }

        static bool HasGeneratedMarker(SyntaxNode node)
        {
            foreach (var trivium in node.GetLeadingTrivia())
            {
                if (trivium.ToString().Contains("<auto-generated"))
                    return true;
            }

            return false;
        }

        public static string EmitExpression(SyntaxNode node)
        {
            var builder = new StringBuilder();
            WriteTokens(builder, node);
            return builder.ToString();
        }

        static void WriteTokens(StringBuilder builder, SyntaxNode node)
        {
            var hasPrevious = false;
            var separated = true;
            var previous = default(SyntaxToken);
            foreach (var token in node.DescendantTokens())
            {
                if (token.LeadingTrivia.Count > 0)
                {
                    WriteTrivia(builder, token.LeadingTrivia);
                    separated = EndsWithSeparator(builder);
                }

                if (hasPrevious && !separated && NeedsSpace(previous, token))
                    builder.Append(' ');

                builder.Append(token.Text);
                if (token.TrailingTrivia.Count > 0)
                {
                    WriteTrivia(builder, token.TrailingTrivia);
                    separated = EndsWithSeparator(builder);
                }
                else
                    separated = false;

                previous = token;
                hasPrevious = true;
            }
        }

        static void WriteTrivia(StringBuilder builder, SyntaxTriviaList trivia)
        {
            foreach (var trivium in trivia)
            {
                if (trivium.IsDirective)
                    WriteDirective(builder, trivium);
                else
                    builder.Append(trivium.ToString());
            }
        }

        // Directive tokens are built without trivia, so ToFullString glues "#nullable" to "enable"
        // and the line no longer parses as a preprocessor directive.
        static void WriteDirective(StringBuilder builder, SyntaxTrivia trivium)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
                builder.Append('\n');

            var structure = trivium.GetStructure();
            if (structure == null)
            {
                builder.Append(trivium.ToFullString());
            }
            else
            {
                var hasPrevious = false;
                var previous = default(SyntaxToken);
                foreach (var token in structure.DescendantTokens())
                {
                    if (token.Text.Length == 0)
                        continue;
                    if (hasPrevious && NeedsSpace(previous, token))
                        builder.Append(' ');
                    builder.Append(token.Text);
                    previous = token;
                    hasPrevious = true;
                }
            }

            if (builder.Length == 0 || builder[builder.Length - 1] != '\n')
                builder.Append('\n');
        }

        static bool EndsWithSeparator(StringBuilder builder)
        {
            if (builder.Length == 0)
                return true;
            var last = builder[builder.Length - 1];
            return last is ' ' or '\n' or '\r' or '\t';
        }

        static bool NeedsSpace(SyntaxToken previous, SyntaxToken next)
        {
            var before = previous.Text;
            var after = next.Text;
            if (before.Length == 0 || after.Length == 0)
                return false;

            var end = before[before.Length - 1];
            var start = after[0];
            if (end is '(' or '[' or '.' or '!' or '~' or '{' or '<')
                return false;
            if (start is ')' or ']' or '.' or ',' or ';' or ':' or '}' or '>' or '<')
                return false;
            if (end == '?' && (char.IsLetterOrDigit(start) || start == '_'))
                return true;
            if (char.IsLetterOrDigit(end) || end is '_' or '>')
                return char.IsLetterOrDigit(start) || start == '_';
            if (end is ')' or ']' && (char.IsLetterOrDigit(start) || start == '_'))
                return true;
            if (after == "=>" || before == "=>")
                return true;
            if (end == ',' && start != ')')
                return true;
            if (end == ':' && start != ':' && (char.IsLetterOrDigit(start) || start == '_'))
                return true;
            if (start == '?' && (char.IsLetterOrDigit(end) || end is ')' or ']'))
                return true;

            return false;
        }
    }
}