using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stunts.CodeAnalysis;

namespace Stunts
{
    /// <summary>A generated stunt, with the assemblies that request it at run time.</summary>
    sealed class GeneratedStunt
    {
        public GeneratedStunt(string name, INamedTypeSymbol[] types, SyntaxNode source, SyntaxNode syntax)
            => (Name, Types, Source, Syntax) = (name, types, source, syntax);

        public string Name { get; }

        public INamedTypeSymbol[] Types { get; }

        public SyntaxNode Source { get; }

        public SyntaxNode Syntax { get; set; }

        public SortedSet<string> Assemblies { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Emits the module initializer that registers the typed factories of generated stunts 
    /// with the <c>CompiledStuntFactory</c>, and the stunt definitions of the externally visible 
    /// generic wrappers so referencing projects can close them.
    /// </summary>
    static class StuntRegistrations
    {
        const string ModuleInitializer = "System.Runtime.CompilerServices.ModuleInitializerAttribute";
        static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <summary>
        /// Adds the registration of each stunt, nesting forwarders in the containing types 
        /// of stunts that cannot be accessed from the top-level registration class.
        /// </summary>
        public static string? Register(ProcessorContext context, NamingConvention naming, IReadOnlyList<GeneratedStunt> stunts)
        {
            if (stunts.Count == 0)
                return null;

            var options = (CSharpParseOptions)context.ParseOptions;
            var trees = stunts.ToDictionary(stunt => stunt,
                stunt => CSharpSyntaxTree.ParseText(stunt.Syntax.NormalizeWhitespace().ToFullString(), options, cancellationToken: context.CancellationToken));
            var compilation = context.Compilation.AddSyntaxTrees(trees.Values);
            var assemblyName = context.Compilation.Assembly.Name;
            var body = new StringBuilder();

            for (var index = 0; index < stunts.Count; index++)
            {
                var stunt = stunts[index];
                var tree = trees[stunt];
                var root = tree.GetRoot(context.CancellationToken);
                var shortName = naming.GetName(stunt.Types);
                var declaration = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault(type => type.Identifier.ValueText == shortName && type.BaseList != null);
                if (declaration == null ||
                    compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol symbol)
                    continue;

                var statements = Statements(compilation, symbol, stunt, assemblyName).ToArray();
                if (statements.Length == 0)
                    continue;

                var containers = declaration.Ancestors().OfType<TypeDeclarationSyntax>().ToArray();
                if (containers.Length == 0)
                {
                    foreach (var statement in statements)
                        body.Append("            ").AppendLine(statement);
                    continue;
                }

                // The stunt is only accessible from its containing types, so each one forwards 
                // the registration to the next, starting with the outermost.
                var method = "__RegisterStunt" + index;
                var updated = root.ReplaceNodes(containers, (original, rewritten) =>
                {
                    var inner = Array.IndexOf(containers, original) - 1;
                    var member = inner < 0
                        ? $"internal static void {method}() {{ {string.Join(" ", statements)} }}"
                        : $"internal static void {method}() => {containers[inner].Identifier.ValueText}.{method}();";
                    return rewritten.AddMembers((MemberDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(member, options: options)!);
                });

                stunt.Syntax = updated;
                var outermost = (INamedTypeSymbol)symbol.ContainingType!;
                while (outermost.ContainingType != null)
                    outermost = outermost.ContainingType;

                body.Append("            ").Append(outermost.ToDisplayString(TypeFormat)).Append('.').Append(method).AppendLine("();");
            }

            if (body.Length == 0)
                return null;

            var code = new StringBuilder()
                .AppendLine("// <auto-generated />")
                .AppendLine("#nullable disable")
                .AppendLine("#pragma warning disable CA2255")
                .AppendLine("namespace Stunts.Generated")
                .AppendLine("{")
                .AppendLine("    static partial class StuntRegistrations")
                .AppendLine("    {")
                .AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]")
                .AppendLine("        internal static void Initialize()")
                .AppendLine("        {")
                .Append(body)
                .AppendLine("        }")
                .AppendLine("    }")
                .AppendLine("}");

            if (context.Compilation.GetTypeByMetadataName(ModuleInitializer) is not INamedTypeSymbol attribute ||
                !context.Compilation.IsSymbolAccessibleWithin(attribute, context.Compilation.Assembly))
            {
                code.AppendLine()
                    .AppendLine("namespace System.Runtime.CompilerServices")
                    .AppendLine("{")
                    .AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]")
                    .AppendLine("    sealed class ModuleInitializerAttribute : global::System.Attribute { }")
                    .AppendLine("}");
            }

            return code.ToString();
        }

        static IEnumerable<string> Statements(Compilation compilation, INamedTypeSymbol symbol, GeneratedStunt stunt, string assemblyName)
        {
            var stuntName = symbol.ToDisplayString(TypeFormat);
            var types = "new global::System.Type[] { " + string.Join(", ", stunt.Types.Select(type => $"typeof({type.ToDisplayString(TypeFormat)})")) + " }";
            var delegateType = stunt.Types[0].TypeKind == TypeKind.Delegate ? stunt.Types[0].ToDisplayString(TypeFormat) : null;
            var within = (ISymbol?)symbol.ContainingType ?? compilation.Assembly;

            var constructors = new List<string>();
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var expanded = new List<(string Signature, string Constructor)>();
            foreach (var constructor in symbol.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) ||
                    constructor.Parameters.Any(parameter =>
                        parameter.RefKind is not (RefKind.None or RefKind.In) ||
                        parameter.Type.IsRefLikeType ||
                        parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Error ||
                        !compilation.IsSymbolAccessibleWithin(parameter.Type, within)) ||
                    IsObsoleteError(constructor) ||
                    RequiresMemberInitializer(constructor))
                    continue;

                var parameterTypes = constructor.Parameters.Select(parameter => parameter.Type.ToDisplayString(TypeFormat)).ToArray();
                var signature = string.Join(", ", parameterTypes);
                signatures.Add(signature);
                constructors.Add(Constructor(parameterTypes, parameterTypes.Select((type, index) => $"({type})args[{index}]")));

                // Like C#, a params array can be omitted entirely, unless another constructor has that signature.
                if (constructor.Parameters.LastOrDefault() is { IsParams: true, Type: IArrayTypeSymbol array })
                {
                    var head = parameterTypes.Take(parameterTypes.Length - 1).ToArray();
                    expanded.Add((string.Join(", ", head), Constructor(head, head
                        .Select((type, index) => $"({type})args[{index}]")
                        .Concat(new[] { $"global::System.Array.Empty<{array.ElementType.ToDisplayString(TypeFormat)}>()" }))));
                }
            }

            constructors.AddRange(expanded.Where(x => signatures.Add(x.Signature)).Select(x => x.Constructor));

            string Constructor(string[] parameterTypes, IEnumerable<string> arguments)
            {
                var creation = $"new {stuntName}({string.Join(", ", arguments)})";
                if (delegateType != null)
                    creation = $"new {delegateType}({creation}.Invoke)";

                return "new global::Stunts.StuntConstructor(new global::System.Type[] { " +
                    string.Join(", ", parameterTypes.Select(type => $"typeof({type})")) +
                    $" }}, args => {creation})";
            }

            if (constructors.Count == 0)
                yield break;

            foreach (var assembly in stunt.Assemblies)
            {
                var requester = assembly == assemblyName
                    ? $"typeof({stuntName}).Assembly"
                    : SymbolDisplay.FormatLiteral(assembly, true);

                yield return $"global::Stunts.CompiledStuntFactory.Register({requester}, typeof({stuntName}), {types}, {string.Join(", ", constructors)});";
            }
        }

        /// <summary>
        /// Emits the stunt definitions of the externally visible generic wrappers in the compilation, 
        /// which <see cref="StuntClosure"/> reads to close them in referencing projects.
        /// </summary>
        public static string? Definitions(ProcessorContext context, StuntClosure closure, IEnumerable<IMethodSymbol> methods)
        {
            if (context.Compilation.GetTypeByMetadataName(StuntClosure.DefinitionAttributeName) == null)
                return null;

            var assemblyName = context.Compilation.Assembly.Name;
            var members = new StringBuilder();
            var index = 0;
            foreach (var method in methods.Distinct<IMethodSymbol>(SymbolEqualityComparer.Default))
            {
                if (!method.IsGenericMethod || !IsExternallyVisible(method) ||
                    DocumentationCommentId.CreateDeclarationId(method) is not string id)
                    continue;

                var result = closure.GetDefinitions(method, context.CancellationToken);
                if (result.Error != StuntClosureError.None || result.IsLeaf)
                    continue;

                // The stunt definition of a method that just forwards its own type parameters is implied.
                if (result.Definitions.Length == 1 && result.Definitions[0].Assembly == assemblyName &&
                    result.Definitions[0].Types.SequenceEqual<ITypeSymbol>(method.TypeParameters.Cast<ITypeSymbol>(), SymbolEqualityComparer.Default))
                    continue;

                var typeParameters = string.Join(", ", method.TypeParameters.Select(parameter => parameter.Name));
                var constraints = Constraints(method.TypeParameters);
                foreach (var stuntDefinition in result.Definitions.Where(stuntDefinition => stuntDefinition.Types.All(IsPublic)))
                {
                    var parameters = string.Join(", ", stuntDefinition.Types.Select((type, i) => $"{type.ToDisplayString(TypeFormat)} p{i}"));
                    members
                        .Append("        [global::Stunts.StuntDefinition(").Append(SymbolDisplay.FormatLiteral(id, true)).Append(", ")
                        .Append(SymbolDisplay.FormatLiteral(stuntDefinition.Assembly, true)).AppendLine(")]")
                        .Append("        public static void __Definition").Append(index++).Append('<').Append(typeParameters).Append(">(")
                        .Append(parameters).Append(')').Append(constraints).AppendLine(" { }");
                }
            }

            if (members.Length == 0)
                return null;

            return new StringBuilder()
                .AppendLine("// <auto-generated />")
                .AppendLine("#nullable disable")
                .AppendLine("namespace Stunts.Generated")
                .AppendLine("{")
                .AppendLine("    /// <summary>Describes the stunts created by the generic wrappers in this assembly.</summary>")
                .AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]")
                .AppendLine("    public static class StuntDefinitions")
                .AppendLine("    {")
                .Append(members)
                .AppendLine("    }")
                .AppendLine("}")
                .ToString();
        }

        static string Constraints(ImmutableArray<ITypeParameterSymbol> parameters)
        {
            var builder = new StringBuilder();
            foreach (var parameter in parameters)
            {
                var constraints = new List<string>();
                if (parameter.HasUnmanagedTypeConstraint)
                    constraints.Add("unmanaged");
                else if (parameter.HasValueTypeConstraint)
                    constraints.Add("struct");
                else if (parameter.HasReferenceTypeConstraint)
                    constraints.Add("class");
                else if (parameter.HasNotNullConstraint)
                    constraints.Add("notnull");

                // StuntDefinitions is public, so a non-public constraint type cannot appear in the clause.
                // The parameter types still carry the stunt definition.
                constraints.AddRange(parameter.ConstraintTypes.Where(IsPublic).Select(type => type.ToDisplayString(TypeFormat)));
                if (parameter.HasConstructorConstraint)
                    constraints.Add("new()");

                if (constraints.Count > 0)
                    builder.Append(" where ").Append(parameter.Name).Append(" : ").Append(string.Join(", ", constraints));
            }

            return builder.ToString();
        }

        static bool IsExternallyVisible(ISymbol symbol)
        {
            for (var current = symbol; current != null && current is not INamespaceSymbol; current = current.ContainingSymbol)
            {
                if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                    return false;
            }

            return true;
        }

        // An obsolete-as-error constructor can be declared, but the registration is not
        // itself obsolete, so calling it is CS0619. Required members make `new T(...)`
        // illegal unless the constructor is marked SetsRequiredMembers (CS9035).
        static bool IsObsoleteError(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass is not INamedTypeSymbol type ||
                    type.Name != "ObsoleteAttribute" ||
                    type.ContainingNamespace?.ToDisplayString() != "System" ||
                    attribute.ConstructorArguments.Length < 2 ||
                    attribute.ConstructorArguments[1].Value is not true)
                    continue;

                return true;
            }

            return false;
        }

        static bool RequiresMemberInitializer(IMethodSymbol constructor)
        {
            if (HasAttribute(constructor, "System.Runtime.CompilerServices.SetsRequiredMembersAttribute") ||
                HasAttribute(constructor, "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
                return false;

            for (var type = constructor.ContainingType; type != null; type = type.BaseType)
            {
                foreach (var member in type.GetMembers())
                {
                    if (!member.IsStatic && IsRequiredMember(member))
                        return true;
                }
            }

            return false;
        }

        // `required` is a modifier. Source symbols expose it as IsRequired, while
        // metadata symbols also carry RequiredMemberAttribute.
        static bool IsRequiredMember(ISymbol member) => member switch
        {
            IFieldSymbol field => field.IsRequired || HasAttribute(field, "System.Runtime.CompilerServices.RequiredMemberAttribute"),
            IPropertySymbol property => property.IsRequired || HasAttribute(property, "System.Runtime.CompilerServices.RequiredMemberAttribute"),
            _ => false,
        };

        static bool HasAttribute(ISymbol symbol, string metadataName)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass is INamedTypeSymbol type && MetadataName(type) == metadataName)
                    return true;
            }

            return false;
        }

        static string MetadataName(INamedTypeSymbol type)
        {
            var name = type.ContainingType != null ? MetadataName(type.ContainingType) + "+" + type.Name : type.Name;
            var ns = type.ContainingNamespace;
            if (type.ContainingType != null || ns == null || ns.IsGlobalNamespace)
                return name;
            return ns.ToDisplayString() + "." + name;
        }

        static bool IsPublic(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => IsPublic(array.ElementType),
            IPointerTypeSymbol => false,
            INamedTypeSymbol named => named.TypeKind != TypeKind.Error &&
                named.DeclaredAccessibility == Accessibility.Public &&
                (named.ContainingType == null || IsPublic(named.ContainingType)) &&
                named.TypeArguments.All(IsPublic),
            _ => false,
        };
    }
}
