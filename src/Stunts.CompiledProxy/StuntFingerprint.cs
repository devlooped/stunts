using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stunts.CodeAnalysis;
using Stunts.Processors;

namespace Stunts
{
    /// <summary>
    /// Computes the <see cref="StuntCache"/> keys. A key hashes the generator itself, the 
    /// compilation-wide inputs the processors read, and the shape of the stunt's types: 
    /// their declarations and members, including inherited ones, and the declarations of 
    /// the types their signatures and attributes use. Member bodies and unrelated types 
    /// are not part of it, so editing them reuses the generated stunts.
    /// </summary>
    sealed class StuntFingerprint
    {
        /// <summary>Format of type names stored in cache entries.</summary>
        public static readonly SymbolDisplayFormat NameFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        static readonly SymbolDisplayFormat MemberFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef |
                SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeAccessibility |
                SymbolDisplayMemberOptions.IncludeConstantValue,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeExtensionThis,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName);

        readonly Compilation compilation;
        // Stunts bound on the user compilation, and those bound on its treeless copy (see MetadataScaffold).
        readonly string environment;
        readonly string metadataEnvironment;
        readonly ConcurrentDictionary<ITypeSymbol, string> declarations = new(SymbolEqualityComparer.IncludeNullability);
        readonly ConcurrentDictionary<ITypeSymbol, string> references = new(SymbolEqualityComparer.IncludeNullability);
        readonly ConcurrentDictionary<INamedTypeSymbol, string> attributeClasses = new(SymbolEqualityComparer.Default);
        readonly ConcurrentDictionary<IAssemblySymbol, string> assemblies = new(SymbolEqualityComparer.Default);

        public StuntFingerprint(ProcessorContext context, StuntGenerator generator, IEnumerable<ISyntaxProcessor> processors, NamingConvention naming, bool pretty)
        {
            compilation = context.Compilation;
            var shared = Environment(context, generator, processors, naming, pretty);
            environment = Hash(shared + Lookups(compilation));
            metadataEnvironment = Hash(shared + Lookups(compilation.RemoveAllSyntaxTrees()));
        }

        /// <summary>
        /// Whether the output of <paramref name="processors"/> is fully described by the fingerprint. 
        /// Processors from other assemblies can read anything from the context.
        /// </summary>
        public static bool Cacheable(IEnumerable<ISyntaxProcessor> processors)
            => processors.All(processor => processor.GetType().Assembly == typeof(StuntGenerator).Assembly);

        /// <summary>Folder name for the generator version, shared by every compilation it runs on.</summary>
        public static string Version(StuntGenerator generator, IEnumerable<ISyntaxProcessor> processors)
        {
            var builder = new StringBuilder("1");
            var assemblies = new HashSet<Assembly>
            {
                typeof(StuntGenerator).Assembly,
                typeof(NamingConvention).Assembly,
                typeof(CSharpSyntaxTree).Assembly,
                typeof(Compilation).Assembly,
                generator.GetType().Assembly,
                generator.NamingConvention.GetType().Assembly,
            };
            foreach (var processor in processors)
                assemblies.Add(processor.GetType().Assembly);

            foreach (var assembly in assemblies.OrderBy(assembly => assembly.FullName, StringComparer.Ordinal))
                builder.Append('|').Append(assembly.FullName).Append(':').Append(assembly.ManifestModule.ModuleVersionId.ToString("N"));

            return Hash(builder.ToString());
        }

        // The inputs every stunt of a compilation shares: the generator configuration, the 
        // language and runtime features the processors check, and the target framework.
        static string Environment(ProcessorContext context, StuntGenerator generator, IEnumerable<ISyntaxProcessor> processors, NamingConvention naming, bool pretty)
        {
            var compilation = context.Compilation;
            var builder = new StringBuilder()
                .Append(generator.GetType().FullName).Append('|')
                .Append(generator.GeneratorAttribute.FullName).Append('|')
                .Append(string.Join(",", processors.Select(processor => processor is DefaultImports imports
                    ? processor.GetType().FullName + "(" + string.Join(" ", imports.Namespaces) + ")"
                    : processor.GetType().FullName))).Append('|')
                // Imports and interface names are sorted with the current culture.
                .Append(CultureInfo.CurrentCulture.Name).Append('|')
                .Append(naming.GetType().FullName).Append(':').Append(naming.RootNamespace).Append(':')
                .Append(naming.NameSuffix).Append(':').Append(naming.DefaultImplementationPrefix).Append('|')
                .Append(context.Language).Append('|')
                .Append(((CSharpParseOptions)context.ParseOptions).LanguageVersion).Append('|')
                .Append((compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions)?.LanguageVersion).Append('|')
                .Append(compilation.Options.NullableContextOptions).Append('|')
                .Append(compilation.Options.MetadataImportOptions).Append('|')
                .Append((compilation.Options as CSharpCompilationOptions)?.AllowUnsafe).Append('|')
                .Append(pretty).Append('|')
                .Append(compilation.SupportsRuntimeCapability(RuntimeCapability.ByRefLikeGenerics)).Append('|')
                .Append(compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly?.Identity.GetDisplayName()).Append('|');

            return builder.ToString();
        }

        // Optional framework and runtime types whose presence changes the generated code, as
        // seen from the compilation that binds the stunt. A source polyfill is not in the treeless one.
        static string Lookups(Compilation compilation)
        {
            var builder = new StringBuilder();
            foreach (var name in new[]
            {
                "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
                "Stunts.IStunt",
            })
            {
                var type = compilation.GetTypeByMetadataName(name);
                builder.Append(name).Append('=')
                    .Append(type == null ? "-" : compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) + ":" + type.ContainingAssembly?.Identity.GetDisplayName())
                    .Append('|');
            }

            return builder.ToString();
        }

        /// <summary>
        /// The key of the stunt that implements <paramref name="types"/>, in the order given, which
        /// is the order of the base list. <paramref name="metadata"/> stunts bind without user source.
        /// <paramref name="names"/> are the generated names its text uses that collisions can change.
        /// </summary>
        public string Stunt(INamedTypeSymbol[] types, bool metadata, string names)
        {
            var builder = new StringBuilder(metadata ? metadataEnvironment : environment).Append("|stunt|").Append(names);
            foreach (var type in types)
                builder.Append('|').Append(Declaration(type));

            return Hash(builder.ToString());
        }

        /// <summary>The key of the class <paramref name="name"/> with the default member implementations of <paramref name="type"/>.</summary>
        public string Default(INamedTypeSymbol type, string name)
            => Hash(environment + "|default|" + name + "|" + Declaration(type));

        /// <summary>The name of a type in a cache entry.</summary>
        public static string Name(ITypeSymbol type) => type.ToDisplayString(NameFormat);

        // The type with all its members, base types and interfaces.
        string Declaration(ITypeSymbol type)
        {
            if (declarations.TryGetValue(type, out var hash))
                return hash;

            var builder = new StringBuilder(Reference(type));
            if (type is INamedTypeSymbol named)
            {
                Attributes(builder, named.GetAttributes());
                TypeParameters(builder, named.TypeParameters);
                for (var container = named.ContainingType; container != null; container = container.ContainingType)
                {
                    builder.Append("|in ").Append(Reference(container));
                    TypeParameters(builder, container.TypeParameters);
                }
            }

            if (type.BaseType != null)
                builder.Append("|base ").Append(Declaration(type.BaseType));

            foreach (var iface in type.Interfaces)
                builder.Append("|implements ").Append(Declaration(iface));

            foreach (var member in type.GetMembers())
            {
                if (member is not ITypeSymbol)
                    Member(builder.Append('\n'), member);
            }

            return declarations.GetOrAdd(type, Hash(builder.ToString()));
        }

        // What a signature or attribute that uses the type can observe: its identity, kind, 
        // accessibility from this compilation, and its base types and interfaces.
        string Reference(ITypeSymbol type)
        {
            if (references.TryGetValue(type, out var hash))
                return hash;

            var builder = new StringBuilder(type.ToDisplayString(MemberFormat))
                .Append('|').Append(type.TypeKind)
                .Append('|').Append(type.SpecialType)
                .Append('|').Append(type.NullableAnnotation)
                .Append('|').Append(Flag(type.IsReferenceType)).Append(Flag(type.IsValueType)).Append(Flag(type.IsRefLikeType))
                .Append(Flag(type.IsReadOnly)).Append(Flag(type.IsUnmanagedType)).Append(Flag(type.IsRecord)).Append(Flag(type.IsTupleType))
                .Append(Flag(type.IsNativeIntegerType)).Append(Flag(type.IsAnonymousType)).Append(Flag(type.IsStatic))
                .Append(Flag(type.IsAbstract)).Append(Flag(type.IsSealed)).Append(Flag(type.CanBeReferencedByName))
                .Append('|').Append(Assembly(type.ContainingAssembly));

            for (ISymbol? current = type; current is INamedTypeSymbol; current = current.ContainingType)
                builder.Append('|').Append(current.DeclaredAccessibility);

            switch (type)
            {
                case IArrayTypeSymbol array:
                    builder.Append("|array ").Append(array.Rank).Append(' ').Append(Reference(array.ElementType));
                    break;
                case IPointerTypeSymbol pointer:
                    builder.Append("|pointer ").Append(Reference(pointer.PointedAtType));
                    break;
                case ITypeParameterSymbol parameter:
                    // Constraints can name the parameter itself, so they are display text only.
                    builder.Append("|parameter ").Append(parameter.Ordinal).Append(' ').Append(parameter.TypeParameterKind);
                    break;
                case INamedTypeSymbol named:
                    builder.Append("|arity ").Append(named.Arity).Append(Flag(named.IsGenericType)).Append(Flag(named.IsUnboundGenericType));
                    foreach (var argument in named.TypeArguments)
                        builder.Append("|argument ").Append(Reference(argument));
                    if (named.DelegateInvokeMethod is IMethodSymbol invoke)
                        builder.Append("|invoke ").Append(invoke.ToDisplayString(MemberFormat));
                    if (named.EnumUnderlyingType is INamedTypeSymbol underlying)
                        builder.Append("|enum ").Append(underlying.SpecialType);
                    break;
            }

            for (var current = type.BaseType; current != null; current = current.BaseType)
                builder.Append("|base ").Append(current.ToDisplayString(MemberFormat));
            foreach (var iface in type.AllInterfaces)
                builder.Append("|interface ").Append(iface.ToDisplayString(MemberFormat));

            return references.GetOrAdd(type, Hash(builder.ToString()));
        }

        void Member(StringBuilder builder, ISymbol member)
        {
            builder.Append(member.Kind).Append(' ').Append(member.MetadataName).Append(' ')
                .Append(member.ToDisplayString(MemberFormat))
                .Append('|').Append(member.DeclaredAccessibility)
                .Append('|').Append(Flag(member.IsStatic)).Append(Flag(member.IsAbstract)).Append(Flag(member.IsVirtual))
                .Append(Flag(member.IsOverride)).Append(Flag(member.IsSealed)).Append(Flag(member.IsExtern))
                .Append(Flag(member.IsImplicitlyDeclared)).Append(Flag(member.CanBeReferencedByName))
                .Append('|').Append(Assembly(member.ContainingAssembly));
            Attributes(builder, member.GetAttributes());

            switch (member)
            {
                case IMethodSymbol method:
                    builder.Append("|method ").Append(method.MethodKind).Append(' ').Append(method.RefKind)
                        .Append(Flag(method.IsReadOnly)).Append(Flag(method.IsInitOnly)).Append(Flag(method.IsVararg))
                        .Append(Flag(method.IsGenericMethod)).Append(Flag(method.ReturnsVoid)).Append(' ')
                        .Append(Reference(method.ReturnType));
                    Attributes(builder.Append("|return"), method.GetReturnTypeAttributes());
                    TypeParameters(builder, method.TypeParameters);
                    Parameters(builder, method.Parameters);
                    foreach (var implemented in method.ExplicitInterfaceImplementations)
                        builder.Append("|explicit ").Append(implemented.ToDisplayString(MemberFormat));
                    if (method.OverriddenMethod is IMethodSymbol overridden)
                        builder.Append("|overrides ").Append(overridden.ToDisplayString(MemberFormat));
                    break;
                case IPropertySymbol property:
                    builder.Append("|property ").Append(property.RefKind).Append(Flag(property.IsIndexer))
                        .Append(Flag(property.IsRequired)).Append(Flag(property.IsReadOnly)).Append(Flag(property.IsWriteOnly))
                        .Append(Flag(property.IsWithEvents)).Append(' ').Append(Reference(property.Type));
                    Parameters(builder, property.Parameters);
                    foreach (var implemented in property.ExplicitInterfaceImplementations)
                        builder.Append("|explicit ").Append(implemented.ToDisplayString(MemberFormat));
                    if (property.OverriddenProperty is IPropertySymbol overriddenProperty)
                        builder.Append("|overrides ").Append(overriddenProperty.ToDisplayString(MemberFormat));
                    break;
                case IEventSymbol @event:
                    builder.Append("|event ").Append(Reference(@event.Type));
                    foreach (var implemented in @event.ExplicitInterfaceImplementations)
                        builder.Append("|explicit ").Append(implemented.ToDisplayString(MemberFormat));
                    if (@event.OverriddenEvent is IEventSymbol overriddenEvent)
                        builder.Append("|overrides ").Append(overriddenEvent.ToDisplayString(MemberFormat));
                    break;
                case IFieldSymbol field:
                    builder.Append("|field ").Append(Flag(field.IsConst)).Append(Flag(field.IsReadOnly)).Append(Flag(field.IsVolatile))
                        .Append(Flag(field.IsRequired)).Append(Flag(field.IsFixedSizeBuffer)).Append(' ')
                        .Append(field.RefKind).Append(' ').Append(Reference(field.Type));
                    if (field.HasConstantValue)
                        builder.Append("|value ").Append(Constant(field.ConstantValue));
                    break;
            }
        }

        void Parameters(StringBuilder builder, ImmutableArray<IParameterSymbol> parameters)
        {
            foreach (var parameter in parameters)
            {
                builder.Append("|parameter ").Append(parameter.Name).Append(' ').Append(parameter.RefKind).Append(' ')
                    .Append(parameter.ScopedKind).Append(Flag(parameter.IsParams)).Append(Flag(parameter.IsOptional))
                    .Append(Flag(parameter.IsThis)).Append(Flag(parameter.IsDiscard)).Append(' ')
                    .Append(Reference(parameter.Type));
                if (parameter.HasExplicitDefaultValue)
                    builder.Append(" = ").Append(Constant(parameter.ExplicitDefaultValue));
                Attributes(builder, parameter.GetAttributes());
            }
        }

        void TypeParameters(StringBuilder builder, ImmutableArray<ITypeParameterSymbol> parameters)
        {
            foreach (var parameter in parameters)
            {
                builder.Append("|typeparameter ").Append(parameter.Name).Append(' ').Append(parameter.Variance).Append(' ')
                    .Append(Flag(parameter.HasReferenceTypeConstraint)).Append(Flag(parameter.HasValueTypeConstraint))
                    .Append(Flag(parameter.HasUnmanagedTypeConstraint)).Append(Flag(parameter.HasNotNullConstraint))
                    .Append(Flag(parameter.HasConstructorConstraint)).Append(Flag(parameter.AllowsRefLikeType))
                    .Append(' ').Append(parameter.ReferenceTypeConstraintNullableAnnotation);
                foreach (var constraint in parameter.ConstraintTypes)
                    builder.Append(" : ").Append(constraint.ToDisplayString(MemberFormat));
                Attributes(builder, parameter.GetAttributes());
            }
        }

        void Attributes(StringBuilder builder, ImmutableArray<AttributeData> attributes)
        {
            foreach (var attribute in attributes)
            {
                builder.Append("|[");
                var type = attribute.AttributeClass;
                if (type != null)
                    builder.Append(AttributeClass(type));
                if (attribute.AttributeConstructor is IMethodSymbol constructor)
                    builder.Append(' ').Append(constructor.ToDisplayString(MemberFormat)).Append(' ').Append(Assembly(constructor.ContainingAssembly));
                foreach (var argument in attribute.ConstructorArguments)
                    Argument(builder.Append(' '), argument);
                foreach (var argument in attribute.NamedArguments)
                {
                    Argument(builder.Append(' ').Append(argument.Key).Append('='), argument.Value);
                    // Replication checks the named member is settable from the stunt.
                    for (var current = type; current != null; current = current.BaseType)
                    {
                        if (current.GetMembers(argument.Key).FirstOrDefault() is ISymbol member)
                        {
                            builder.Append(':').Append(member.DeclaredAccessibility).Append(':')
                                .Append((member as IPropertySymbol)?.SetMethod?.DeclaredAccessibility).Append(':')
                                .Append(Assembly(member.ContainingAssembly));
                            break;
                        }
                    }
                }
                builder.Append(']');
            }
        }

        // Replication reads the AttributeUsage of the class and of its bases.
        string AttributeClass(INamedTypeSymbol type)
        {
            if (attributeClasses.TryGetValue(type, out var hash))
                return hash;

            var builder = new StringBuilder(Reference(type));
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var usage in current.GetAttributes())
                {
                    if (usage.AttributeClass is { Name: "AttributeUsageAttribute" } usageClass &&
                        usageClass.ContainingNamespace?.ToDisplayString() == "System")
                    {
                        builder.Append("|usage ").Append(current.ToDisplayString(MemberFormat));
                        foreach (var argument in usage.ConstructorArguments)
                            builder.Append(' ').Append(argument.ToCSharpString());
                        foreach (var argument in usage.NamedArguments)
                            builder.Append(' ').Append(argument.Key).Append('=').Append(argument.Value.ToCSharpString());
                    }
                }
            }

            return attributeClasses.GetOrAdd(type, Hash(builder.ToString()));
        }

        void Argument(StringBuilder builder, TypedConstant argument)
        {
            if (argument.Kind == TypedConstantKind.Error)
            {
                builder.Append("error");
                return;
            }

            if (argument.Kind == TypedConstantKind.Array)
            {
                builder.Append(argument.IsNull ? "null" : "{");
                if (!argument.IsNull)
                {
                    foreach (var item in argument.Values)
                        Argument(builder.Append(' '), item);
                    builder.Append('}');
                }
            }
            else
            {
                builder.Append(argument.ToCSharpString());
            }

            // typeof arguments are replicated only when accessible.
            if (argument.Kind == TypedConstantKind.Type && argument.Value is ITypeSymbol value)
                builder.Append(' ').Append(Reference(value));
            if (argument.Type != null)
                builder.Append(':').Append(argument.Type.ToDisplayString(MemberFormat));
        }

        // Only what accessibility checks observe: the same type is internal to one project and
        // visible to a friend. Names and versions are left out, so a rebuilt or renamed
        // assembly keeps its entries.
        string Assembly(IAssemblySymbol? assembly)
        {
            if (assembly == null)
                return "-";

            // Processors bind in compilations derived from this one, with their own assembly symbols.
            return assemblies.GetOrAdd(assembly, assembly =>
                assembly.Identity.Equals(compilation.Assembly.Identity) ? "self" :
                assembly.GivesAccessTo(compilation.Assembly) ? "friend" : "other");
        }

        static string Constant(object? value) => value switch
        {
            null => "null",
            string text => SymbolDisplay.FormatLiteral(text, true),
            char character => SymbolDisplay.FormatLiteral(character, true),
            IFormattable formattable => value.GetType().Name + ":" + formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.GetType().Name + ":" + value,
        };

        static char Flag(bool value) => value ? '1' : '0';

        static string Hash(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(40);
            for (var i = 0; i < 20; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
