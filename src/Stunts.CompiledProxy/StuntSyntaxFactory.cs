using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stunts.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts
{
    abstract class StuntSyntaxFactory
    {
        public static StuntSyntaxFactory CreateFactory(string language)
        {
            if (language != LanguageNames.CSharp)
                throw new NotSupportedException("The only supported language at the moment is " + LanguageNames.CSharp);

            return new CSharpStuntSyntaxFactory();
        }

        public abstract SyntaxNode CreateSyntax(NamingConvention naming, INamedTypeSymbol[] symbols);

        class CSharpStuntSyntaxFactory : StuntSyntaxFactory
        {
            public override SyntaxNode CreateSyntax(NamingConvention naming, INamedTypeSymbol[] symbols)
            {
                var name = naming.GetName(symbols);
                var imports = new HashSet<string>();
                var (baseType, implementedInterfaces) = symbols.ValidateGeneratorTypes();

                if (baseType != null)
                    AddImports(imports, baseType);

                foreach (var iface in implementedInterfaces)
                    AddImports(imports, iface);

                // A delegate cannot be a base class. The stunt is an ordinary class with an
                // Invoke method, and the factory binds a delegate to that method.
                var delegateStunt = baseType?.TypeKind == TypeKind.Delegate;
                var stunt = TypeDeclaration(name, delegateStunt ? null : baseType)
                    .WithModifiers(StuntModifiers(baseType))
                    .WithBaseList(delegateStunt
                        ? BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(ParseTypeName("object"))))
                        : BaseList(SeparatedList<BaseTypeSyntax>(
                            symbols.Select(AsTypeSyntax).Select(type => SimpleBaseType(type)))));

                if (delegateStunt)
                {
                    stunt = stunt.AddMembers(FieldDeclaration(
                        VariableDeclaration(
                            AsTypeSyntax(baseType!),
                            SingletonSeparatedList(VariableDeclarator(Identifier("implementation")))))
                        .WithModifiers(TokenList(Token(SyntaxKind.ReadOnlyKeyword))));
                }

                // A private, protected, or private protected nested type can only be
                // inherited from inside its containing type. The stunt is nested in
                // partial copies of those types, which must already be partial.
                MemberDeclarationSyntax member = stunt;
                var nest = baseType != null && MustNest(baseType);
                if (nest)
                    member = Nest(baseType!.ContainingType!, stunt);

                var unit = CompilationUnit()
                    .WithUsings(List(imports.Select(ns => UsingDirective(ParseName(ns)))));
                var ns = nest
                    ? baseType!.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : null
                    : naming.GetNamespace(symbols);
                return ns == null
                    ? unit.WithMembers(SingletonList(member))
                    : unit.WithMembers(SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(ParseName(ns)).WithMembers(SingletonList(member))));
            }

            void AddImports(HashSet<string> imports, ITypeSymbol symbol)
            {
                if (symbol != null && symbol.ContainingNamespace != null && symbol.ContainingNamespace.CanBeReferencedByName)
                    imports.Add(symbol.ContainingNamespace.ToDisplayString());

                if (symbol is INamedTypeSymbol named && named.IsGenericType)
                {
                    foreach (var typeArgument in named.TypeArguments)
                        AddImports(imports, typeArgument);
                }
            }

            static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

            // Fully qualified so nested types bind in the scaffold compilation.
            // IdentifierName("Outer.Inner") is one identifier and does not.
            TypeSyntax AsTypeSyntax(ITypeSymbol symbol) => ParseTypeName(symbol.ToDisplayString(TypeFormat));

            // A class cannot inherit a record (CS8865). The blank stunt has to be a record
            // when its base is one; interfaces and ordinary classes stay classes.
            static bool MustNest(INamedTypeSymbol type) => NestedTypeStunt.MustNest(type);

            static SyntaxTokenList StuntModifiers(INamedTypeSymbol? baseType)
                => baseType != null && MustNest(baseType)
                    ? TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.PartialKeyword))
                    : TokenList(Token(SyntaxKind.PartialKeyword));

            static MemberDeclarationSyntax Nest(INamedTypeSymbol outer, MemberDeclarationSyntax inner)
            {
                var shell = Shell(outer).WithMembers(SingletonList(inner));
                return outer.ContainingType == null ? shell : Nest(outer.ContainingType, shell);
            }

            static TypeDeclarationSyntax Shell(INamedTypeSymbol type)
            {
                TypeDeclarationSyntax declaration = type.TypeKind == TypeKind.Struct && !type.IsRecord
                    ? StructDeclaration(type.Name)
                    : TypeDeclaration(type.Name, type.IsRecord ? type : null);
                declaration = declaration.WithModifiers(PartialModifiers(type));
                if (type.TypeKind == TypeKind.Struct && type.IsRecord)
                    declaration = ((RecordDeclarationSyntax)declaration).WithClassOrStructKeyword(Token(SyntaxKind.StructKeyword));
                if (type.Arity == 0)
                    return declaration;

                declaration = declaration.WithTypeParameterList(TypeParameterList(SeparatedList(
                    type.TypeParameters.Select(parameter => TypeParameter(parameter.Name)))));
                var clauses = new List<TypeParameterConstraintClauseSyntax>();
                foreach (var parameter in type.TypeParameters)
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
                    foreach (var constraint in parameter.ConstraintTypes)
                        constraints.Add(TypeConstraint(ParseTypeName(constraint.ToDisplayString(TypeFormat))));
                    if (parameter.HasConstructorConstraint)
                        constraints.Add(ConstructorConstraint());
                    if (constraints.Count == 0)
                        continue;
                    clauses.Add(TypeParameterConstraintClause(parameter.Name).WithConstraints(SeparatedList(constraints)));
                }

                return clauses.Count == 0 ? declaration : declaration.WithConstraintClauses(List(clauses));
            }

            static SyntaxTokenList PartialModifiers(INamedTypeSymbol type)
            {
                var tokens = new List<SyntaxToken>();
                switch (type.DeclaredAccessibility)
                {
                    case Accessibility.Public:
                        tokens.Add(Token(SyntaxKind.PublicKeyword));
                        break;
                    case Accessibility.Internal:
                        tokens.Add(Token(SyntaxKind.InternalKeyword));
                        break;
                    case Accessibility.Protected:
                        tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                        break;
                    case Accessibility.Private:
                        tokens.Add(Token(SyntaxKind.PrivateKeyword));
                        break;
                    case Accessibility.ProtectedOrInternal:
                        tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                        tokens.Add(Token(SyntaxKind.InternalKeyword));
                        break;
                    case Accessibility.ProtectedAndInternal:
                        tokens.Add(Token(SyntaxKind.PrivateKeyword));
                        tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                        break;
                }

                tokens.Add(Token(SyntaxKind.PartialKeyword));
                return TokenList(tokens);
            }

            static TypeDeclarationSyntax TypeDeclaration(string name, INamedTypeSymbol? baseType)
            {
                if (baseType?.IsRecord != true)
                    return ClassDeclaration(name);

                return RecordDeclaration(
                        SyntaxKind.RecordDeclaration,
                        Token(SyntaxKind.RecordKeyword),
                        Identifier(name))
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken));
            }
        }
    }
}
