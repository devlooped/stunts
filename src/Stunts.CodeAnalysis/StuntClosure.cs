using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// The stunt types a generator method creates, in terms of its type parameters, 
    /// and the name of the assembly that requests them at run time.
    /// </summary>
    public sealed class StuntDefinition
    {
        /// <summary>Initializes the shape.</summary>
        public StuntDefinition(ImmutableArray<ITypeSymbol> types, string assembly)
            => (Types, Assembly) = (types, assembly);

        /// <summary>The base type (or first interface) and additional interfaces of the stunt.</summary>
        public ImmutableArray<ITypeSymbol> Types { get; }

        /// <summary>The name of the assembly that requests the stunt at run time.</summary>
        public string Assembly { get; }
    }

    /// <summary>Why the shapes of a generator method cannot be determined.</summary>
    public enum StuntClosureError
    {
        /// <summary>The shapes were determined.</summary>
        None,
        /// <summary>A generator invocation uses type parameters of a containing type.</summary>
        ContainingTypeParameter,
        /// <summary>A wrapper can be overridden, so callers cannot know the actual shapes.</summary>
        VirtualWrapper,
        /// <summary>Wrappers invoke each other recursively with different type arguments.</summary>
        Recursive,
        /// <summary>The wrapper chain exceeds <see cref="StuntClosure.MaxDepth"/>.</summary>
        TooDeep,
    }

    /// <summary>The shapes of a generator method definition.</summary>
    public sealed class StuntClosureResult
    {
        internal StuntClosureResult(ImmutableArray<StuntDefinition> shapes, int depth, bool isLeaf, StuntClosureError error = StuntClosureError.None)
            => (Shapes, Depth, IsLeaf, Error) = (shapes, depth, isLeaf, error);

        /// <summary>The stunt shapes, empty if <see cref="Error"/> is not <see cref="StuntClosureError.None"/>.</summary>
        public ImmutableArray<StuntDefinition> Shapes { get; }

        /// <summary>The length of the wrapper chain, where leaf generator methods have a depth of 1.</summary>
        public int Depth { get; }

        /// <summary>Whether the method creates stunts for its own type arguments.</summary>
        public bool IsLeaf { get; }

        /// <summary>The reason the shapes could not be determined, if any.</summary>
        public StuntClosureError Error { get; }
    }

    /// <summary>
    /// Determines the stunt types created by generator methods, closing generic wrappers 
    /// (generator methods passing their own type parameters to other generator methods) 
    /// over the type arguments at each concrete call site.
    /// </summary>
    public sealed class StuntClosure
    {
        /// <summary>Maximum length of a chain of generic wrappers.</summary>
        public const int MaxDepth = 8;

        /// <summary>Metadata name of the type that describes the externally visible generator methods of an assembly.</summary>
        public const string ShapesTypeName = "Stunts.Generated.StuntDefinitions";

        /// <summary>Metadata name of the attribute that describes a shape.</summary>
        public const string ShapeAttributeName = "Stunts.StuntDefinitionAttribute";

        readonly object sync = new();
        readonly Dictionary<IMethodSymbol, StuntClosureResult> results = new(SymbolEqualityComparer.Default);
        readonly HashSet<IMethodSymbol> computing = new(SymbolEqualityComparer.Default);
        readonly Dictionary<SyntaxTree, SemanticModel> models = new();
        readonly Dictionary<IAssemblySymbol, ILookup<string, IMethodSymbol>> metadata = new(SymbolEqualityComparer.Default);

        StuntClosure(Compilation compilation, INamedTypeSymbol generatorAttribute)
            => (Compilation, GeneratorAttribute) = (compilation, generatorAttribute);

        /// <summary>
        /// Creates the closure for the given compilation, or <see langword="null"/> if 
        /// the <paramref name="generatorAttribute"/> is not available in it.
        /// </summary>
        public static StuntClosure? Create(Compilation compilation, Type generatorAttribute)
            => compilation.GetTypeByMetadataName(generatorAttribute.FullName!) is INamedTypeSymbol attribute
                ? new StuntClosure(compilation, attribute) : null;

        /// <summary>The compilation being analyzed.</summary>
        public Compilation Compilation { get; }

        /// <summary>The attribute that flags generator methods.</summary>
        public INamedTypeSymbol GeneratorAttribute { get; }

        /// <summary>Whether the method is annotated with the <see cref="GeneratorAttribute"/>.</summary>
        public bool IsGenerator(IMethodSymbol method)
            => method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, GeneratorAttribute));

        /// <summary>
        /// Gets the stunt types created by an invocation of a generator method with 
        /// concrete type arguments. Returns nothing if any type argument is a type 
        /// parameter (the containing method must be a generator method then).
        /// </summary>
        public IEnumerable<(ImmutableArray<ITypeSymbol> Types, string Assembly)> Close(IMethodSymbol method, CancellationToken cancellation = default)
        {
            if (method.TypeArguments.Any(ContainsTypeParameter) || method.ContainingType?.TypeArguments.Any(ContainsTypeParameter) == true)
                yield break;

            IMethodSymbol definition;
            lock (sync)
                definition = Normalize(method.OriginalDefinition, cancellation);
            var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            for (var i = 0; i < definition.TypeParameters.Length; i++)
                map[definition.TypeParameters[i]] = method.TypeArguments[i];

            foreach (var shape in GetShapes(definition, cancellation).Shapes)
            {
                var types = shape.Types.Select(type => Substitute(type, map)).ToImmutableArray();
                if (types.Length > 0 && !types.Any(ContainsTypeParameter))
                    yield return (types, shape.Assembly);
            }
        }

        /// <summary>Gets the stunt shapes of a generator method definition.</summary>
        public StuntClosureResult GetShapes(IMethodSymbol definition, CancellationToken cancellation = default)
        {
            lock (sync)
                return Compute(Normalize(definition.OriginalDefinition, cancellation), cancellation);
        }

        // Local function symbols are only equal when bound by the same semantic model.
        IMethodSymbol Normalize(IMethodSymbol definition, CancellationToken cancellation)
        {
            if (definition.MethodKind != MethodKind.LocalFunction ||
                definition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellation) is not SyntaxNode syntax ||
                !Compilation.ContainsSyntaxTree(syntax.SyntaxTree))
                return definition;

            return Model(syntax.SyntaxTree).GetDeclaredSymbol(syntax, cancellation) as IMethodSymbol ?? definition;
        }

        SemanticModel Model(SyntaxTree tree)
        {
            if (!models.TryGetValue(tree, out var model))
                models[tree] = model = Compilation.GetSemanticModel(tree);
            return model;
        }

        StuntClosureResult Compute(IMethodSymbol definition, CancellationToken cancellation)
        {
            if (results.TryGetValue(definition, out var result))
                return result;

            if (!computing.Add(definition))
                return new StuntClosureResult(ImmutableArray<StuntDefinition>.Empty, 0, false, StuntClosureError.Recursive);

            try
            {
                result = definition.DeclaringSyntaxReferences.IsEmpty
                    ? FromMetadata(definition)
                    : FromSource(definition, cancellation);
            }
            finally
            {
                computing.Remove(definition);
            }

            results[definition] = result;
            return result;
        }

        StuntClosureResult FromSource(IMethodSymbol definition, CancellationToken cancellation)
        {
            var shapes = new List<StuntDefinition>();
            var depth = 0;
            foreach (var invocation in Invocations(definition, cancellation))
            {
                var target = invocation.TargetMethod;
                if (!IsGenerator(target))
                    continue;

                var arguments = target.TypeArguments;
                if (arguments.Any(argument => References(argument, parameter => parameter.TypeParameterKind == TypeParameterKind.Type)))
                    return Error(StuntClosureError.ContainingTypeParameter);
                if (!arguments.Any(argument => References(argument, parameter => SymbolEqualityComparer.Default.Equals(parameter.DeclaringMethod, definition))))
                    continue;

                // Forwarding the same type parameters to itself adds no shapes.
                if (SymbolEqualityComparer.Default.Equals(target.OriginalDefinition, definition) &&
                    SameTypes(arguments, definition.TypeParameters))
                    continue;

                var inner = Compute(target.OriginalDefinition, cancellation);
                if (inner.Error != StuntClosureError.None)
                    return Error(inner.Error);

                depth = Math.Max(depth, inner.Depth);
                var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
                for (var i = 0; i < target.OriginalDefinition.TypeParameters.Length; i++)
                    map[target.OriginalDefinition.TypeParameters[i]] = arguments[i];

                foreach (var shape in inner.Shapes)
                    shapes.Add(new StuntDefinition(shape.Types.Select(type => Substitute(type, map)).ToImmutableArray(), shape.Assembly));
            }

            if (shapes.Count == 0 && depth == 0)
                return Leaf(definition);

            if (definition.IsVirtual || definition.IsOverride || definition.IsAbstract ||
                definition.ContainingType?.TypeKind == TypeKind.Interface)
                return Error(StuntClosureError.VirtualWrapper);

            // The inner depth includes the leaf generator method, so it equals the wrappers in the chain.
            if (depth > MaxDepth)
                return Error(StuntClosureError.TooDeep);

            return new StuntClosureResult(Distinct(shapes), depth + 1, false);
        }

        StuntClosureResult FromMetadata(IMethodSymbol definition)
        {
            if (!metadata.TryGetValue(definition.ContainingAssembly, out var lookup))
                metadata[definition.ContainingAssembly] = lookup = ShapeMethods(definition.ContainingAssembly);

            var id = DocumentationCommentId.CreateDeclarationId(definition) ?? "";
            var shapes = new List<StuntDefinition>();
            foreach (var method in lookup[id])
            {
                if (method.Arity != definition.Arity)
                    continue;

                var assembly = method.GetAttributes().First(IsShapeAttribute).ConstructorArguments[1].Value as string;
                var constructed = definition.Arity == 0 ? method : method.Construct(definition.TypeParameters.Cast<ITypeSymbol>().ToArray());
                shapes.Add(new StuntDefinition(constructed.Parameters.Select(parameter => parameter.Type).ToImmutableArray(),
                    assembly ?? definition.ContainingAssembly.Name));
            }

            return shapes.Count == 0
                ? Leaf(definition)
                : new StuntClosureResult(Distinct(shapes), 1, shapes.All(shape =>
                    SameTypes(shape.Types, definition.TypeParameters)));
        }

        /// <summary>Gets the shape methods of an assembly, by the documentation id of their generator method.</summary>
        public static ILookup<string, IMethodSymbol> ShapeMethods(IAssemblySymbol assembly)
        {
            if (assembly.GetTypeByMetadataName(ShapesTypeName) is not INamedTypeSymbol type)
                return Array.Empty<IMethodSymbol>().ToLookup(method => "");

            return type.GetMembers().OfType<IMethodSymbol>()
                .Select(method => (Method: method, Attribute: method.GetAttributes().FirstOrDefault(IsShapeAttribute)))
                .Where(pair => pair.Attribute?.ConstructorArguments.Length == 2 && pair.Attribute.ConstructorArguments[0].Value is string)
                .ToLookup(pair => (string)pair.Attribute!.ConstructorArguments[0].Value!, pair => pair.Method);
        }

        /// <summary>
        /// Adds the names of generic generator methods in referenced assemblies.
        /// Identity forwards are omitted from <see cref="ShapeMethods"/>, so an inferred call
        /// would otherwise never be bound.
        /// </summary>
        internal void AddReferencedGeneratorNames(ISet<string> names)
        {
            var attributeAssembly = GeneratorAttribute.ContainingAssembly.Name;
            foreach (var assembly in Compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (!References(assembly, attributeAssembly))
                    continue;

                foreach (var name in GeneratorNames(assembly))
                    names.Add(name);
            }
        }

        IEnumerable<string> GeneratorNames(IAssemblySymbol assembly)
        {
            var stack = new Stack<INamespaceOrTypeSymbol>();
            stack.Push(assembly.GlobalNamespace);
            while (stack.Count > 0)
            {
                foreach (var member in stack.Pop().GetMembers())
                {
                    switch (member)
                    {
                        case INamespaceSymbol space:
                            stack.Push(space);
                            break;
                        case INamedTypeSymbol type:
                            stack.Push(type);
                            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                            {
                                if (method.Arity > 0 && IsGenerator(method))
                                    yield return method.Name;
                            }
                            break;
                    }
                }
            }
        }

        bool References(IAssemblySymbol assembly, string attributeAssembly)
        {
            if (assembly.Name == attributeAssembly)
                return true;

            switch (Compilation.GetMetadataReference(assembly))
            {
                case CompilationReference source:
                    return source.Compilation.AssemblyName == attributeAssembly ||
                        source.Compilation.ReferencedAssemblyNames.Any(identity => identity.Name == attributeAssembly);
                case PortableExecutableReference pe:
                    return MetadataReferences(pe, attributeAssembly);
                default:
                    return true;
            }
        }

        static bool MetadataReferences(PortableExecutableReference pe, string attributeAssembly)
        {
            if (pe.GetMetadata() is not AssemblyMetadata metadata)
                return true;

            foreach (var module in metadata.GetModules())
            {
                var reader = module.GetMetadataReader();
                foreach (var handle in reader.AssemblyReferences)
                {
                    if (reader.GetString(reader.GetAssemblyReference(handle).Name) == attributeAssembly)
                        return true;
                }
            }

            return false;
        }

        static bool SameTypes(ImmutableArray<ITypeSymbol> types, ImmutableArray<ITypeParameterSymbol> parameters)
            => types.Length == parameters.Length && types.Zip(parameters, (type, parameter) => SymbolEqualityComparer.Default.Equals(type, parameter)).All(same => same);

        static bool IsShapeAttribute(AttributeData attribute)
            => attribute.AttributeClass?.ToDisplayString() == ShapeAttributeName;

        static StuntClosureResult Leaf(IMethodSymbol definition)
            => new(definition.Arity == 0 ? ImmutableArray<StuntDefinition>.Empty : ImmutableArray.Create(
                new StuntDefinition(definition.TypeParameters.Cast<ITypeSymbol>().ToImmutableArray(), definition.ContainingAssembly.Name)), 1, true);

        static StuntClosureResult Error(StuntClosureError error)
            => new(ImmutableArray<StuntDefinition>.Empty, 0, false, error);

        static ImmutableArray<StuntDefinition> Distinct(List<StuntDefinition> shapes)
            => shapes.GroupBy(shape => shape.Assembly + "|" + string.Join(",", shape.Types.Select(type => type.ToDisplayString())))
                .Select(group => group.First())
                .ToImmutableArray();

        IEnumerable<IInvocationOperation> Invocations(IMethodSymbol definition, CancellationToken cancellation)
        {
            foreach (var reference in definition.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax(cancellation);
                if (!Compilation.ContainsSyntaxTree(syntax.SyntaxTree))
                    continue;

                var model = Model(syntax.SyntaxTree);

                // VB declares methods on the statement, the operation is on the enclosing block.
                var operation = model.GetOperation(syntax, cancellation) ??
                    (syntax.Parent is SyntaxNode parent ? model.GetOperation(parent, cancellation) : null);
                if (operation == null)
                    continue;

                foreach (var invocation in operation.DescendantsAndSelf().OfType<IInvocationOperation>())
                    yield return invocation;
            }
        }

        /// <summary>Replaces the type parameters in <paramref name="type"/> with their mapped type arguments.</summary>
        public ITypeSymbol Substitute(ITypeSymbol type, IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> map)
        {
            switch (type)
            {
                case ITypeParameterSymbol parameter:
                    return map.TryGetValue(parameter, out var argument) ? argument : parameter;
                case IArrayTypeSymbol array:
                    return Compilation.CreateArrayTypeSymbol(Substitute(array.ElementType, map), array.Rank);
                case IPointerTypeSymbol pointer:
                    return Compilation.CreatePointerTypeSymbol(Substitute(pointer.PointedAtType, map));
                case INamedTypeSymbol named when ContainsTypeParameter(named):
                    var definition = named.OriginalDefinition;
                    if (named.ContainingType is INamedTypeSymbol container && ContainsTypeParameter(container) &&
                        Substitute(container, map) is INamedTypeSymbol substituted)
                        definition = substituted.GetTypeMembers(named.Name, named.Arity).FirstOrDefault() ?? definition;

                    return definition.Arity == 0
                        ? definition
                        : definition.Construct(named.TypeArguments.Select(argument => Substitute(argument, map)).ToArray());
                default:
                    return type;
            }
        }

        /// <summary>Whether the type is or contains a type parameter.</summary>
        public static bool ContainsTypeParameter(ITypeSymbol type)
            => References(type, _ => true);

        /// <summary>Whether the type is or contains a type parameter matching <paramref name="predicate"/>.</summary>
        public static bool References(ITypeSymbol type, Func<ITypeParameterSymbol, bool> predicate) => type switch
        {
            ITypeParameterSymbol parameter => predicate(parameter),
            IArrayTypeSymbol array => References(array.ElementType, predicate),
            IPointerTypeSymbol pointer => References(pointer.PointedAtType, predicate),
            INamedTypeSymbol named => named.TypeArguments.Any(argument => References(argument, predicate)) ||
                (named.ContainingType != null && References(named.ContainingType, predicate)),
            _ => false,
        };
    }
}
