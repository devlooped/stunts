using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts
{
    static class SyntaxExtensions
    {
        public static bool HasAttribute(this SyntaxList<AttributeListSyntax> attributes, string attribute)
            => attributes.Count > 0 &&
                attributes.Any(list => list.Attributes.Any(attr => attr.Name.ToString() == attribute));

        public static bool IsKind(this SyntaxToken token, SyntaxKind kind)
            => token.RawKind == (int)kind;

        public static bool IsRefOut(this ArgumentSyntax argument)
            => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword);

        public static bool IsOut(this ArgumentSyntax argument)
            => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword);

        public static bool IsOut(this ParameterSyntax parameter)
            => parameter.Modifiers.Any(SyntaxKind.OutKeyword);

        public static bool IsRefOut(this ParameterSyntax parameter)
            => parameter.Modifiers.Any(SyntaxKind.RefKeyword) || parameter.Modifiers.Any(SyntaxKind.OutKeyword);

        public static bool IsVoid(this TypeSyntax? typeSyntax)
            => typeSyntax == null ||
               typeSyntax.IsKind(SyntaxKind.PredefinedType) &&
                ((PredefinedTypeSyntax)typeSyntax).Keyword.IsKind(SyntaxKind.VoidKeyword);

        public static InvocationExpressionSyntax WithArguments(this InvocationExpressionSyntax invocation, params ArgumentSyntax[] arguments)
            => WithArguments(invocation, (IEnumerable<ArgumentSyntax>)arguments);

        public static InvocationExpressionSyntax WithArguments(this InvocationExpressionSyntax invocation, IEnumerable<ArgumentSyntax> arguments)
            => invocation.WithArgumentList(ArgumentList(SeparatedList(arguments)));

        public static ElementAccessExpressionSyntax WithArgumentList(this ElementAccessExpressionSyntax indexer, params ArgumentSyntax[] arguments)
            => WithArgumentList(indexer, (IEnumerable<ArgumentSyntax>)arguments);

        public static ElementAccessExpressionSyntax WithArgumentList(this ElementAccessExpressionSyntax indexer, IEnumerable<ArgumentSyntax> arguments)
            => indexer.WithArgumentList(BracketedArgumentList(SeparatedList(arguments)));

        public static ConstructorDeclarationSyntax WithSemicolon(this ConstructorDeclarationSyntax syntax)
            => syntax.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        public static PropertyDeclarationSyntax WithSemicolon(this PropertyDeclarationSyntax syntax)
            => syntax.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        public static AccessorDeclarationSyntax WithSemicolon(this AccessorDeclarationSyntax syntax)
            => syntax.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        public static IndexerDeclarationSyntax WithSemicolon(this IndexerDeclarationSyntax syntax)
            => syntax.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        public static MethodDeclarationSyntax WithSemicolon(this MethodDeclarationSyntax syntax)
            => syntax.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }
}
