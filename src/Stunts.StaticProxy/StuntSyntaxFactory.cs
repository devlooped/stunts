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

                return CompilationUnit()
                    .WithUsings(
                        List(
                            imports.Select(ns => UsingDirective(ParseName(ns)))))
                    .WithMembers(
                        SingletonList<MemberDeclarationSyntax>(
                            NamespaceDeclaration(ParseName(naming.GetNamespace(symbols)))
                            .WithMembers(
                                SingletonList<MemberDeclarationSyntax>(
                                    TypeDeclaration(name, baseType)
                                    .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)))
                                    .WithBaseList(
                                        BaseList(
                                            SeparatedList<BaseTypeSyntax>(
                                                symbols.Select(AsTypeSyntax).Select(t => SimpleBaseType(t)))))))));
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
