using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Stunts.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.Processors
{
    /// <summary>
    /// Fills the blank stunt class with constructors, interface implementations, and
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
            // The stunt is the type with the base list. Enclosing partials, used so a
            // private nested type can be inherited, have no base list.
            var declaration = syntax.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(type => type.BaseList != null && type is ClassDeclarationSyntax or RecordDeclarationSyntax);
            if (model == null || declaration == null)
                return syntax;

            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol symbol)
                return syntax;

            var original = declaration;
            if (ProxiedType(declaration, model) is INamedTypeSymbol proxied)
            {
                var copied = AttributeReplication.Replicate(proxied, AttributeTargets.Class, symbol.ContainingAssembly, includeInherited: false);
                if (copied.Count > 0)
                    declaration = declaration.WithAttributeLists(declaration.AttributeLists.AddRange(copied));
            }

            var members = new List<MemberDeclarationSyntax>();
            members.AddRange(Constructors(symbol));

            var generated = new Dictionary<string, ISymbol>();
            foreach (var member in symbol.GetOverridableMembers(context.CancellationToken))
            {
                if (generated.ContainsKey(ParameterSignature(member)))
                    continue;
                generated.Add(ParameterSignature(member), member);
                members.Add(Stub(member, isOverride: true, explicitInterface: null, stunt: symbol, assembly: symbol.ContainingAssembly));
            }

            members.AddRange(InterfaceStubs(symbol, generated, context.CancellationToken));

            foreach (var (member, provider) in symbol.GetDefaultImplementedInterfaceMembers(context.CancellationToken))
            {
                context.DefaultImplementations.Add(provider);
                var instance = DefaultInstance(context.NamingConvention, provider, member.ContainingType);
                members.Add(Stub(member, false, Collides(member, generated), instance, symbol, symbol.ContainingAssembly));
            }

            return syntax.ReplaceNode(original, declaration.AddMembers(members.ToArray()));
        }

        // The first base is the class, or the primary interface when the stunt has no class base.
        static INamedTypeSymbol? ProxiedType(TypeDeclarationSyntax declaration, SemanticModel model)
        {
            var first = declaration.BaseList?.Types.FirstOrDefault();
            if (first == null)
                return null;
            return model.GetTypeInfo(first.Type).Type as INamedTypeSymbol;
        }

        /// <summary>
        /// Members of the interfaces the <paramref name="symbol"/> has not implemented yet, 
        /// throwing <see cref="System.NotImplementedException"/>.
        /// </summary>
        internal static IEnumerable<MemberDeclarationSyntax> InterfaceStubs(INamedTypeSymbol symbol, Dictionary<string, ISymbol> generated, CancellationToken cancellationToken)
        {
            var assembly = symbol.ContainingAssembly;
            foreach (var member in symbol.GetUnimplementedInterfaceMembers(cancellationToken))
                yield return Stub(member, isOverride: false, explicitInterface: Collides(member, generated), assembly: assembly);
        }

        // Same parameters, different return type: IEnumerable.GetEnumerator vs IEnumerable<T>.GetEnumerator.
        // The first one stays public, the rest are implemented explicitly.
        static INamedTypeSymbol? Collides(ISymbol member, Dictionary<string, ISymbol> generated)
        {
            var key = ParameterSignature(member);
            if (generated.ContainsKey(key))
                return member.ContainingType;

            generated.Add(key, member);
            return null;
        }

        // => global::Stunts.DefaultIFoo.Default, cast to the member's interface when a derived 
        // interface provides the default for a base interface member.
        static ExpressionSyntax DefaultInstance(NamingConvention naming, INamedTypeSymbol provider, INamedTypeSymbol iface)
        {
            ExpressionSyntax instance = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseName("global::" + naming.GetDefaultImplementationFullName(provider)),
                IdentifierName(DefaultImplementation.InstanceName));

            if (SymbolEqualityComparer.Default.Equals(provider, iface))
                return instance;

            return ParenthesizedExpression(CastExpression(TypeName(iface), instance));
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

                var parameters = constructor.Parameters.Select(parameter => Parameter(parameter, symbol.ContainingAssembly)).ToArray();
                var initializer = ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(constructor.Parameters.Select(ArgumentFor))));

                yield return ConstructorDeclaration(symbol.Name)
                    .WithAttributeLists(AttributeReplication.Replicate(constructor, AttributeTargets.Constructor, symbol.ContainingAssembly, includeInherited: true))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithInitializer(initializer)
                    .WithBody(Block());
            }
        }

        static bool Accessible(IMethodSymbol constructor, INamedTypeSymbol stunt)
        {
            switch (constructor.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return true;
                case Accessibility.Internal:
                    return constructor.ContainingAssembly.GivesAccessTo(stunt.ContainingAssembly);
                case Accessibility.ProtectedAndInternal:
                    return SymbolEqualityComparer.Default.Equals(constructor.ContainingAssembly, stunt.ContainingAssembly);
                default:
                    return false;
            }
        }

        // A null receiver throws NotImplementedException. Otherwise, the member proceeds to 
        // base (overrides) or to the default instance (default interface implementations).
        static MemberDeclarationSyntax Stub(ISymbol member, bool isOverride, INamedTypeSymbol? explicitInterface, ExpressionSyntax? defaultInstance = null, INamedTypeSymbol? stunt = null, IAssemblySymbol? assembly = null)
        {
            assembly ??= stunt?.ContainingAssembly ?? member.ContainingAssembly;
            var receiver = defaultInstance ?? (isOverride && !member.IsAbstract ? BaseExpression() : null);
            if (member is IMethodSymbol method)
                return Method(method, isOverride, explicitInterface, receiver, assembly);
            if (member is IPropertySymbol property)
                return property.IsIndexer ? Indexer(property, isOverride, explicitInterface, receiver, stunt, assembly) : Property(property, isOverride, explicitInterface, receiver, stunt, assembly);
            return Event((IEventSymbol)member, isOverride, explicitInterface, defaultInstance, assembly);
        }

        static MethodDeclarationSyntax Method(IMethodSymbol method, bool isOverride, INamedTypeSymbol? explicitInterface, ExpressionSyntax? receiver, IAssemblySymbol assembly)
        {
            var declaration = MethodDeclaration(ReturnType(method.ReturnType, method), method.Name)
                .WithAttributeLists(AttributeReplication.Replicate(method, AttributeTargets.Method, assembly, includeInherited: false)
                    .AddRange(AttributeReplication.ReplicateReturn(method, assembly)))
                .WithModifiers(Modifiers(method, isOverride, explicitInterface != null))
                .WithParameterList(ParameterList(SeparatedList(method.Parameters.Select(parameter => Parameter(parameter, assembly)))))
                .WithExpressionBody(ArrowExpressionClause(Body(method, receiver)))
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

            return Annotate(declaration, method.ReturnType);
        }

        static PropertyDeclarationSyntax Property(IPropertySymbol property, bool isOverride, INamedTypeSymbol? explicitInterface, ExpressionSyntax? receiver, INamedTypeSymbol? stunt, IAssemblySymbol assembly)
        {
            var declaration = PropertyDeclaration(ReturnType(property.Type, property.GetMethod), property.Name)
                .WithAttributeLists(MemberAttributes(property, property.GetMethod, stunt, assembly))
                .WithModifiers(Modifiers(property, isOverride, explicitInterface != null))
                .WithAccessorList(AccessorList(List(Accessors(property, receiver, stunt, assembly))));
            if (explicitInterface != null)
                declaration = declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
            return Annotate(declaration, property.Type);
        }

        static IndexerDeclarationSyntax Indexer(IPropertySymbol property, bool isOverride, INamedTypeSymbol? explicitInterface, ExpressionSyntax? receiver, INamedTypeSymbol? stunt, IAssemblySymbol assembly)
        {
            var declaration = IndexerDeclaration(ReturnType(property.Type, property.GetMethod))
                .WithAttributeLists(MemberAttributes(property, property.GetMethod, stunt, assembly))
                .WithModifiers(Modifiers(property, isOverride, explicitInterface != null))
                .WithParameterList(BracketedParameterList(SeparatedList(property.Parameters.Select(parameter => Parameter(parameter, assembly)))))
                .WithAccessorList(AccessorList(List(Accessors(property, receiver, stunt, assembly))));
            if (explicitInterface != null)
                declaration = declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
            return Annotate(declaration, property.Type);
        }

        static EventDeclarationSyntax Event(IEventSymbol ev, bool isOverride, INamedTypeSymbol? explicitInterface, ExpressionSyntax? defaultInstance, IAssemblySymbol assembly)
        {
            AccessorDeclarationSyntax Accessor(SyntaxKind kind, SyntaxKind assignment, IMethodSymbol? method)
            {
                var accessor = AccessorDeclaration(kind)
                    .WithAttributeLists(method == null
                        ? default
                        : AttributeReplication.Replicate(method, AttributeTargets.Method, assembly, includeInherited: false));
                if (defaultInstance == null)
                    return accessor.WithBody(Block());

                return accessor
                    .WithExpressionBody(ArrowExpressionClause(Proceed(defaultInstance, AssignmentExpression(
                        assignment,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, defaultInstance, IdentifierName(ev.Name)),
                        IdentifierName("value")))))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            var declaration = EventDeclaration(TypeName(ev.Type), ev.Name)
                .WithAttributeLists(AttributeReplication.Replicate(ev, AttributeTargets.Event, assembly, includeInherited: true))
                .WithModifiers(Modifiers(ev, isOverride, explicitInterface != null))
                .WithAccessorList(AccessorList(List(new[]
                {
                    Accessor(SyntaxKind.AddAccessorDeclaration, SyntaxKind.AddAssignmentExpression, ev.AddMethod),
                    Accessor(SyntaxKind.RemoveAccessorDeclaration, SyntaxKind.SubtractAssignmentExpression, ev.RemoveMethod),
                })));
            return explicitInterface == null
                ? declaration
                : declaration.WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(ParseName(explicitInterface.ToDisplayString(TypeFormat))));
        }

        // Property and event attributes are not visible on an override. Accessor methods are.
        static SyntaxList<AttributeListSyntax> MemberAttributes(IPropertySymbol property, IMethodSymbol? getter, INamedTypeSymbol? stunt, IAssemblySymbol assembly)
        {
            var lists = AttributeReplication.Replicate(property, AttributeTargets.Property, assembly, includeInherited: true);
            if (getter != null && OverridableAccessor(getter, stunt))
                lists = lists.AddRange(AttributeReplication.ReplicateReturn(getter, assembly));
            return lists;
        }

        static IEnumerable<AccessorDeclarationSyntax> Accessors(IPropertySymbol property, ExpressionSyntax? receiver, INamedTypeSymbol? stunt, IAssemblySymbol assembly)
        {
            if (property.GetMethod != null && OverridableAccessor(property.GetMethod, stunt))
            {
                ExpressionSyntax value = receiver != null
                    ? Proceed(receiver, property.IsIndexer
                        ? (ExpressionSyntax)ElementAccess(receiver, property)
                        : MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(property.Name)))
                    : ThrowNotImplemented();
                if (property.GetMethod.ReturnsByRef || property.GetMethod.ReturnsByRefReadonly)
                    value = RefExpression(value);
                yield return AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithAttributeLists(AttributeReplication.Replicate(property.GetMethod, AttributeTargets.Method, assembly, includeInherited: false))
                    .WithModifiers(NarrowedModifiers(property, property.GetMethod))
                    .WithExpressionBody(ArrowExpressionClause(value))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            if (property.SetMethod != null && OverridableAccessor(property.SetMethod, stunt))
            {
                ExpressionSyntax value = receiver != null
                    ? Proceed(receiver, property.IsIndexer
                        ? AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, ElementAccess(receiver, property), IdentifierName("value"))
                        : AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(property.Name)),
                            IdentifierName("value")))
                    : ThrowNotImplemented();
                yield return AccessorDeclaration(SetterKind(property.SetMethod))
                    .WithAttributeLists(AttributeReplication.Replicate(property.SetMethod, AttributeTargets.Method, assembly, includeInherited: false))
                    .WithModifiers(NarrowedModifiers(property, property.SetMethod))
                    .WithExpressionBody(ArrowExpressionClause(value))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
        }

        // An override cannot widen an accessor (CS0507). Private, and internal or
        // private protected from another assembly, are not part of the override.
        static bool OverridableAccessor(IMethodSymbol accessor, INamedTypeSymbol? stunt)
        {
            switch (accessor.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return true;
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    return stunt != null && SymbolEqualityComparer.Default.Equals(accessor.ContainingAssembly, stunt.ContainingAssembly);
                default:
                    return false;
            }
        }

        static SyntaxTokenList NarrowedModifiers(ISymbol property, IMethodSymbol accessor)
        {
            if (accessor.DeclaredAccessibility == property.DeclaredAccessibility)
                return TokenList();

            switch (accessor.DeclaredAccessibility)
            {
                case Accessibility.Protected:
                    return TokenList(Token(SyntaxKind.ProtectedKeyword));
                case Accessibility.Internal:
                    return TokenList(Token(SyntaxKind.InternalKeyword));
                case Accessibility.ProtectedOrInternal:
                    return TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword));
                case Accessibility.ProtectedAndInternal:
                    return TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword));
                default:
                    return TokenList();
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

        static ExpressionSyntax Body(IMethodSymbol method, ExpressionSyntax? receiver)
        {
            if (receiver == null)
                return ThrowNotImplemented();

            var access = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(method.Name));
            if (method.IsGenericMethod)
            {
                access = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    receiver,
                    GenericName(method.Name).WithTypeArgumentList(TypeArgumentList(SeparatedList(
                        method.TypeArguments.Select(TypeName)))));
            }

            ExpressionSyntax invocation = Proceed(receiver, InvocationExpression(access, ArgumentList(SeparatedList(method.Parameters.Select(ArgumentFor)))));
            if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                invocation = RefExpression(invocation);
            return invocation;
        }

        // CSharpRewrite finds base calls by the base keyword. Calls to a default 
        // implementation are flagged with an annotation instead.
        static TExpression Proceed<TExpression>(ExpressionSyntax receiver, TExpression expression) where TExpression : ExpressionSyntax
            => receiver is BaseExpressionSyntax ? expression :
                expression.WithAdditionalAnnotations(Annotations.DefaultImplementation);

        static ElementAccessExpressionSyntax ElementAccess(ExpressionSyntax receiver, IPropertySymbol property)
            => ElementAccessExpression(
                receiver,
                BracketedArgumentList(SeparatedList(property.Parameters.Select(ArgumentFor))));

        static TypeSyntax ReturnType(ITypeSymbol type, IMethodSymbol? accessor)
        {
            var syntax = TypeName(type);
            if (accessor == null)
                return syntax;
            // RefKind.RefReadOnly and RefKind.In share the same value, so use the bools.
            if (accessor.ReturnsByRefReadonly)
                return RefType(syntax).WithReadOnlyKeyword(Token(SyntaxKind.ReadOnlyKeyword));
            if (accessor.ReturnsByRef)
                return RefType(syntax);
            return syntax;
        }

        static TNode Annotate<TNode>(TNode node, ITypeSymbol type) where TNode : SyntaxNode
        {
            if (type.IsRefLikeType)
                return (TNode)node.WithAdditionalAnnotations(Annotations.StructRef);
            if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
                return (TNode)node.WithAdditionalAnnotations(Annotations.PointerRef);
            return node;
        }

        static ParameterSyntax Parameter(IParameterSymbol parameter, IAssemblySymbol assembly)
        {
            var syntax = SyntaxFactory.Parameter(Identifier(parameter.Name))
                .WithAttributeLists(AttributeReplication.Replicate(parameter, AttributeTargets.Parameter, assembly, includeInherited: true)
                    .AddRange(AttributeReplication.Defaults(parameter)))
                .WithType(TypeName(parameter.Type));
            var modifiers = new List<SyntaxToken>();
            switch (parameter.RefKind)
            {
                case Microsoft.CodeAnalysis.RefKind.Ref:
                    modifiers.Add(Token(SyntaxKind.RefKeyword));
                    break;
                case Microsoft.CodeAnalysis.RefKind.Out:
                    modifiers.Add(Token(SyntaxKind.OutKeyword));
                    break;
                case Microsoft.CodeAnalysis.RefKind.In:
                    modifiers.Add(Token(SyntaxKind.InKeyword));
                    break;
                case Microsoft.CodeAnalysis.RefKind.RefReadOnlyParameter:
                    modifiers.Add(Token(SyntaxKind.RefKeyword));
                    modifiers.Add(Token(SyntaxKind.ReadOnlyKeyword));
                    break;
            }

            if (parameter.IsParams)
                modifiers.Add(Token(SyntaxKind.ParamsKeyword));
            if (modifiers.Count > 0)
                syntax = syntax.WithModifiers(TokenList(modifiers));
            return Annotate(syntax, parameter.Type);
        }

        static ArgumentSyntax ArgumentFor(IParameterSymbol parameter)
        {
            var argument = Argument(IdentifierName(parameter.Name));
            SyntaxKind? kind = parameter.RefKind switch
            {
                Microsoft.CodeAnalysis.RefKind.Ref => SyntaxKind.RefKeyword,
                Microsoft.CodeAnalysis.RefKind.Out => SyntaxKind.OutKeyword,
                Microsoft.CodeAnalysis.RefKind.In => SyntaxKind.InKeyword,
                Microsoft.CodeAnalysis.RefKind.RefReadOnlyParameter => SyntaxKind.InKeyword,
                _ => null,
            };
            return kind == null ? argument : argument.WithRefKindKeyword(Token(kind.Value));
        }

        internal static TypeSyntax TypeName(ITypeSymbol type) => ParseTypeName(type.ToDisplayString(TypeFormat));

        static SyntaxTokenList Modifiers(ISymbol symbol, bool isOverride, bool explicitInterface)
        {
            if (explicitInterface)
                return TokenList();

            // An override cannot change accessibility (CS0507).
            var tokens = new List<SyntaxToken>();
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Protected:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    break;
                case Accessibility.Internal:
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    break;
                case Accessibility.ProtectedOrInternal:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    break;
                case Accessibility.ProtectedAndInternal:
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
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
