using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Avatars.Processors
{
    /// <summary>
    /// Fills the blank avatar class with constructors, interface implementations, and
    /// overrides. Replaces the AdhocWorkspace code-fix scaffold.
    /// </summary>
    /// <remarks>
    /// Virtual members call <c>base</c>. Abstract and interface members throw
    /// <see cref="System.NotImplementedException"/>. <see cref="CSharpRewrite"/> turns
    /// both shapes into pipeline invocations.
    /// </remarks>
    public class MemberScaffold : ISyntaxProcessor
    {
        static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <inheritdoc/>
        public string Language => LanguageNames.CSharp;

        /// <inheritdoc/>
        public ProcessorPhase Phase => ProcessorPhase.Scaffold;

        /// <inheritdoc/>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
        {
            var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
            var declaration = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (model == null || declaration == null)
                return syntax;

            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol symbol)
                return syntax;

            var members = new List<MemberDeclarationSyntax>();
            members.AddRange(Constructors(symbol));

            var generated = new Dictionary<string, ISymbol>();
            foreach (var member in symbol.GetOverridableMembers(context.CancellationToken))
            {
                if (generated.ContainsKey(ParameterSignature(member)))
                    continue;
                generated.Add(ParameterSignature(member), member);
                members.Add(Stub(member, isOverride: true, explicitInterface: null));
            }

            foreach (var member in symbol.GetUnimplementedInterfaceMembers(context.CancellationToken))
            {
                var key = ParameterSignature(member);
                if (generated.ContainsKey(key))
                {
                    // Same parameters, different return type: IEnumerable.GetEnumerator vs IEnumerable<T>.GetEnumerator.
                    members.Add(Stub(member, isOverride: false, explicitInterface: member.ContainingType));
                    continue;
                }

                generated.Add(key, member);
                members.Add(Stub(member, isOverride: false, explicitInterface: null));
            }

            return syntax.ReplaceNode(declaration, declaration.AddMembers(members.ToArray()));
        }

        static IEnumerable<ConstructorDeclarationSyntax> Constructors(INamedTypeSymbol symbol)
        {
            var baseType = symbol.BaseType;
            if (baseType == null)
                yield break;

            foreach (var constructor in baseType.Constructors)
            {
                if (constructor.MethodKind != MethodKind.Constructor || constructor.IsStatic)
                    continue;
                if (!Accessible(constructor, symbol))
                    continue;

                var parameters = constructor.Parameters.Select(Parameter).ToArray();
                var initializer = ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(constructor.Parameters.Select(ArgumentFor))));

                yield return ConstructorDeclaration(symbol.Name)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithInitializer(initializer)
                    .WithBody(Block());
            }
        }

        static bool Accessible(IMethodSymbol constructor, INamedTypeSymbol avatar)
        {
            switch (constructor.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return true;
                case Accessibility.Internal:
                    return SymbolEqualityComparer.Default.Equals(constructor.ContainingAssembly, avatar.ContainingAssembly);
                default:
                    return false;
            }
        }

        static MemberDeclarationSyntax Stub(ISymbol member, bool isOverride, INamedTypeSymbol? explicitInterface)
        {
            if (member is IMethodSymbol method)
                return Method(method, isOverride, explicitInterface);
            if (member is IPropertySymbol property)
                return property.IsIndexer ? Indexer(property, isOverride, explicitInterface) : Property(property, isOverride, explicitInterface);
            return Event((IEventSymbol)member, isOverride, explicitInterface);
        }

        static MethodDeclarationSyntax Method(IMethodSymbol method, bool isOverride, INamedTypeSymbol? explicitInterface)
        {
            var declaration = MethodDeclaration(TypeName(method.ReturnType), method.Name)
                .WithModifiers(Modifiers(method, isOverride, explicitInterface != null))
                .WithParameterList(ParameterList(SeparatedList(method.Parameters.Select(Parameter))))
                .WithExpressionBody(ArrowExpressionClause(Body(method, isOverride && !method.IsAbstract)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            if (explicitInterface != null)
                declaration = declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));

            if (method.TypeParameters.Length > 0)
            {
                declaration = declaration
                    .WithTypeParameterList(TypeParameterList(SeparatedList(
                        method.TypeParameters.Select(parameter => TypeParameter(parameter.Name)))))
                    .WithConstraintClauses(Constraints(method));
            }

            if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                declaration = declaration.WithReturnType(RefType(declaration.ReturnType));

            return declaration;
        }

        static PropertyDeclarationSyntax Property(IPropertySymbol property, bool isOverride, INamedTypeSymbol? explicitInterface)
        {
            var declaration = PropertyDeclaration(TypeName(property.Type), property.Name)
                .WithModifiers(Modifiers(property, isOverride, explicitInterface != null))
                .WithAccessorList(AccessorList(List(Accessors(property, isOverride))));
            return explicitInterface == null
                ? declaration
                : declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
        }

        static IndexerDeclarationSyntax Indexer(IPropertySymbol property, bool isOverride, INamedTypeSymbol? explicitInterface)
        {
            var declaration = IndexerDeclaration(TypeName(property.Type))
                .WithModifiers(Modifiers(property, isOverride, explicitInterface != null))
                .WithParameterList(BracketedParameterList(SeparatedList(property.Parameters.Select(Parameter))))
                .WithAccessorList(AccessorList(List(Accessors(property, isOverride))));
            return explicitInterface == null
                ? declaration
                : declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
        }

        static EventDeclarationSyntax Event(IEventSymbol ev, bool isOverride, INamedTypeSymbol? explicitInterface)
        {
            var declaration = EventDeclaration(TypeName(ev.Type), ev.Name)
                .WithModifiers(Modifiers(ev, isOverride, explicitInterface != null))
                .WithAccessorList(AccessorList(List(new[]
                {
                    AccessorDeclaration(SyntaxKind.AddAccessorDeclaration).WithBody(Block()),
                    AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration).WithBody(Block()),
                })));
            return explicitInterface == null
                ? declaration
                : declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
        }

        static IEnumerable<AccessorDeclarationSyntax> Accessors(IPropertySymbol property, bool isOverride)
        {
            var callBase = isOverride && !property.IsAbstract;
            if (property.GetMethod != null)
            {
                ExpressionSyntax value = callBase
                    ? property.IsIndexer
                        ? (ExpressionSyntax)BaseElementAccess(property)
                        : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, BaseExpression(), IdentifierName(property.Name))
                    : ThrowNotImplemented();
                if (property.GetMethod.ReturnsByRef || property.GetMethod.ReturnsByRefReadonly)
                    value = RefExpression(value);
                yield return AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithExpressionBody(ArrowExpressionClause(value))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            if (property.SetMethod != null && property.SetMethod.DeclaredAccessibility != Accessibility.Private)
            {
                ExpressionSyntax value = callBase
                    ? property.IsIndexer
                        ? AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, BaseElementAccess(property), IdentifierName("value"))
                        : AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, BaseExpression(), IdentifierName(property.Name)),
                            IdentifierName("value"))
                    : ThrowNotImplemented();
                var accessor = AccessorDeclaration(SetterKind(property.SetMethod))
                    .WithExpressionBody(ArrowExpressionClause(value))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                if (property.SetMethod.DeclaredAccessibility != property.DeclaredAccessibility &&
                    property.SetMethod.DeclaredAccessibility == Accessibility.Protected)
                    accessor = accessor.WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword)));
                yield return accessor;
            }
        }

        static SyntaxKind SetterKind(IMethodSymbol setter)
            => setter.IsInitOnly ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration;

        static SyntaxList<TypeParameterConstraintClauseSyntax> Constraints(IMethodSymbol method)
        {
            var clauses = new List<TypeParameterConstraintClauseSyntax>();
            foreach (var parameter in method.TypeParameters)
            {
                var constraints = new List<TypeParameterConstraintSyntax>();
                if (parameter.HasUnmanagedTypeConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("unmanaged")));
                else if (parameter.HasValueTypeConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("struct")));
                else if (parameter.HasReferenceTypeConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("class")));
                else if (parameter.HasNotNullConstraint)
                    constraints.Add(TypeConstraint(IdentifierName("notnull")));

                foreach (var type in parameter.ConstraintTypes)
                    constraints.Add(TypeConstraint(TypeName(type)));
                if (parameter.HasConstructorConstraint)
                    constraints.Add(ConstructorConstraint());
                if (constraints.Count == 0)
                    continue;

                clauses.Add(TypeParameterConstraintClause(parameter.Name).WithConstraints(SeparatedList(constraints)));
            }

            return List(clauses);
        }

        static ExpressionSyntax Body(IMethodSymbol method, bool callBase)
        {
            if (!callBase)
                return ThrowNotImplemented();

            var access = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, BaseExpression(), IdentifierName(method.Name));
            if (method.IsGenericMethod)
            {
                access = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    BaseExpression(),
                    GenericName(method.Name).WithTypeArgumentList(TypeArgumentList(SeparatedList(
                        method.TypeArguments.Select(TypeName)))));
            }

            ExpressionSyntax invocation = InvocationExpression(access, ArgumentList(SeparatedList(method.Parameters.Select(ArgumentFor))));
            if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                invocation = RefExpression(invocation);
            return invocation;
        }

        static ElementAccessExpressionSyntax BaseElementAccess(IPropertySymbol property)
            => ElementAccessExpression(
                BaseExpression(),
                BracketedArgumentList(SeparatedList(property.Parameters.Select(ArgumentFor))));

        static ParameterSyntax Parameter(IParameterSymbol parameter)
        {
            var syntax = SyntaxFactory.Parameter(Identifier(parameter.Name)).WithType(TypeName(parameter.Type));
            var kind = RefKind(parameter.RefKind);
            if (kind != null)
                syntax = syntax.WithModifiers(TokenList(Token(kind.Value)));
            if (parameter.IsParams)
                syntax = syntax.AddModifiers(Token(SyntaxKind.ParamsKeyword));
            return syntax;
        }

        static ArgumentSyntax ArgumentFor(IParameterSymbol parameter)
        {
            var argument = Argument(IdentifierName(parameter.Name));
            var kind = RefKind(parameter.RefKind);
            return kind == null ? argument : argument.WithRefKindKeyword(Token(kind.Value));
        }

        static SyntaxKind? RefKind(RefKind kind)
        {
            switch (kind)
            {
                case Microsoft.CodeAnalysis.RefKind.Ref: return SyntaxKind.RefKeyword;
                case Microsoft.CodeAnalysis.RefKind.Out: return SyntaxKind.OutKeyword;
                case Microsoft.CodeAnalysis.RefKind.In: return SyntaxKind.InKeyword;
                default: return null;
            }
        }

        static TypeSyntax TypeName(ITypeSymbol type) => ParseTypeName(type.ToDisplayString(TypeFormat));

        static SyntaxTokenList Modifiers(ISymbol symbol, bool isOverride, bool explicitInterface)
        {
            if (explicitInterface)
                return TokenList();

            // Overrides cannot widen accessibility. Protected-or-internal across
            // assemblies is emitted as protected, which is the legal override.
            var tokens = new List<SyntaxToken>();
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    break;
                default:
                    tokens.Add(Token(SyntaxKind.PublicKeyword));
                    break;
            }

            if (isOverride)
                tokens.Add(Token(SyntaxKind.OverrideKeyword));
            return TokenList(tokens);
        }

        static ThrowExpressionSyntax ThrowNotImplemented()
            => ThrowExpression(ObjectCreationExpression(ParseTypeName("global::System.NotImplementedException")).WithArgumentList(ArgumentList()));

        static string ParameterSignature(ISymbol member)
        {
            if (member is IMethodSymbol method)
                return method.Name + "(" + string.Join(",", method.Parameters.Select(parameter => parameter.Type.ToDisplayString())) + ")";
            if (member is IPropertySymbol property && property.IsIndexer)
                return "this(" + string.Join(",", property.Parameters.Select(parameter => parameter.Type.ToDisplayString())) + ")";
            return member.Kind + ":" + member.Name;
        }
    }
}
