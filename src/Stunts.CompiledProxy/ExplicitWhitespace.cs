using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Stunts
{
    /// <summary>
    /// Attaches newline and indent trivia to generated declarations.
    /// This is the layout <see cref="SyntaxNode.NormalizeWhitespace"/> would infer,
    /// written onto the nodes instead of rebuilt for every token.
    /// </summary>
    static class ExplicitWhitespace
    {
        const int MaxIndent = 12;
        static readonly SyntaxTriviaList[] Indents = CreateIndents();

        public static SyntaxNode Apply(SyntaxNode root) => new Rewriter().Visit(root)!;

        static SyntaxTriviaList[] CreateIndents()
        {
            var indents = new SyntaxTriviaList[MaxIndent];
            var spaces = "";
            indents[0] = SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed);
            for (var level = 1; level < MaxIndent; level++)
            {
                spaces += "    ";
                indents[level] = SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(spaces));
            }

            return indents;
        }

        static SyntaxTriviaList IndentOf(int level) => Indents[Math.Min(level, MaxIndent - 1)];

        static SyntaxToken Lead(SyntaxToken token, int level)
            => token.IsKind(SyntaxKind.None) ? token : token.WithLeadingTrivia(IndentOf(level));

        sealed class Rewriter : CSharpSyntaxRewriter
        {
            int indent;

            public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
            {
                var header = node.GetLeadingTrivia();
                node = (CompilationUnitSyntax)base.VisitCompilationUnit(node.WithLeadingTrivia(SyntaxFactory.TriviaList()))!;
                if (header.Count > 0)
                    node = node.WithLeadingTrivia(header.AddRange(node.GetLeadingTrivia()));
                return node;
            }

            public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
                => base.VisitUsingDirective(node)!.WithLeadingTrivia(IndentOf(0));

            public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
            {
                var level = indent;
                indent++;
                node = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;
                indent--;
                return node.WithLeadingTrivia(IndentOf(level))
                    .WithOpenBraceToken(Lead(node.OpenBraceToken, level))
                    .WithCloseBraceToken(Lead(node.CloseBraceToken, level));
            }

            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
                => VisitType(node, base.VisitClassDeclaration);

            public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
                => VisitType(node, base.VisitStructDeclaration);

            public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
                => VisitType(node, base.VisitRecordDeclaration);

            public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
                => VisitType(node, base.VisitInterfaceDeclaration);

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
                => VisitMember(node, base.VisitMethodDeclaration);

            public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
                => VisitMember(node, base.VisitConstructorDeclaration);

            public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
                => VisitMember(node, base.VisitPropertyDeclaration);

            public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
                => VisitMember(node, base.VisitIndexerDeclaration);

            public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
                => VisitMember(node, base.VisitFieldDeclaration);

            public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
                => VisitMember(node, base.VisitEventDeclaration);

            public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
                => VisitMember(node, base.VisitEventFieldDeclaration);

            public override SyntaxNode? VisitBlock(BlockSyntax node)
            {
                var level = indent;
                indent++;
                var statements = new StatementSyntax[node.Statements.Count];
                for (var i = 0; i < node.Statements.Count; i++)
                    statements[i] = (StatementSyntax)Visit(node.Statements[i])!.WithLeadingTrivia(IndentOf(indent));
                indent--;
                return node.WithStatements(SyntaxFactory.List(statements))
                    .WithOpenBraceToken(Lead(node.OpenBraceToken, level))
                    .WithCloseBraceToken(Lead(node.CloseBraceToken, level));
            }

            public override SyntaxNode? VisitAccessorList(AccessorListSyntax node)
            {
                var level = indent;
                indent++;
                var accessors = new AccessorDeclarationSyntax[node.Accessors.Count];
                for (var i = 0; i < node.Accessors.Count; i++)
                    accessors[i] = (AccessorDeclarationSyntax)Visit(node.Accessors[i])!.WithLeadingTrivia(IndentOf(indent));
                indent--;
                return node.WithAccessors(SyntaxFactory.List(accessors))
                    .WithOpenBraceToken(Lead(node.OpenBraceToken, level))
                    .WithCloseBraceToken(Lead(node.CloseBraceToken, level));
            }

            T VisitType<T>(T node, Func<T, SyntaxNode?> visit)
                where T : BaseTypeDeclarationSyntax
            {
                var level = indent;
                indent++;
                node = (T)visit(node)!;
                indent--;
                return (T)node.WithLeadingTrivia(IndentOf(level))
                    .WithOpenBraceToken(Lead(node.OpenBraceToken, level))
                    .WithCloseBraceToken(Lead(node.CloseBraceToken, level));
            }

            T VisitMember<T>(T node, Func<T, SyntaxNode?> visit)
                where T : SyntaxNode
            {
                var level = indent;
                return (T)visit(node)!.WithLeadingTrivia(IndentOf(level));
            }
        }
    }
}
