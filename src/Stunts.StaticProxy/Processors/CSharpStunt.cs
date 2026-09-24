using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Stunts.SyntaxFactoryGenerator;

namespace Stunts.Processors
{
    /// <summary>
    /// Adds the <see cref="IStunt"/> interface implementation.
    /// </summary>
    public class CSharpStunt : ISyntaxProcessor
    {
        /// <summary>
        /// Applies to <see cref="LanguageNames.CSharp"/>.
        /// </summary>
        public string Language { get; } = LanguageNames.CSharp;

        /// <summary>
        /// Runs in the final phase, <see cref="ProcessorPhase.Fixup"/>.
        /// </summary>
        public ProcessorPhase Phase => ProcessorPhase.Fixup;

        /// <summary>
        /// Adds the <see cref="IStunt"/> interface implementation to the document.
        /// </summary>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
            => new CSharpStuntVisitor().Visit(syntax)!;

        class CSharpStuntVisitor : CSharpSyntaxRewriter
        {
            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

                if (node.BaseList != null && !node.BaseList.Types.Any(x =>
                    x.ToString() == nameof(IStunt) ||
                    x.ToString() == typeof(IStunt).FullName))
                {
                    // Only add the base type if it isn't already there
                    node = node.AddBaseListTypes(SimpleBaseType(IdentifierName(nameof(IStunt))));
                }

                if (!node.Members.OfType<PropertyDeclarationSyntax>().Any(prop => prop.Identifier.ToString() == nameof(IStunt.Behaviors)))
                {
                    var behaviors = PropertyDeclaration(
                        GenericName(
                            "IList",
                            IdentifierName(nameof(IStuntBehavior))),
                        Identifier(nameof(IStunt.Behaviors)))
                        .WithExplicitInterfaceSpecifier(
                            ExplicitInterfaceSpecifier(
                                IdentifierName(nameof(IStunt))))
                        .WithExpressionBody(
                            ArrowExpressionClause(
                                MemberAccessExpression(
                                    IdentifierName("pipeline"),
                                    IdentifierName("Behaviors"))))
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                        .NormalizeWhitespace()
                        .WithTrailingTrivia(CarriageReturnLineFeed, CarriageReturnLineFeed);

                    if (node.Members.Count > 0)
                        node = node.InsertNodesAfter(node.Members.First(), new[] { behaviors });
                    else
                        node = node.AddMembers(behaviors);
                }

                if (!node.Members.OfType<FieldDeclarationSyntax>().Any(x => x.Declaration.Variables.Any(v => v.Identifier.ToString() == "pipeline")))
                {
                    node = node.InsertNodesBefore(node.Members.First(), new[]
                    {
                        FieldDeclaration(
                            VariableDeclaration(
                                "pipeline",
                                IdentifierName(nameof(BehaviorPipeline)),
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            nameof(BehaviorPipelineFactory),
                                            nameof(BehaviorPipelineFactory.Default)),
                                        GenericName(
                                            nameof(IBehaviorPipelineFactory.CreatePipeline),
                                            IdentifierName(node.Identifier.ValueText)))))
                            .NormalizeWhitespace()
                        ).WithModifiers(TokenList(Token(SyntaxKind.ReadOnlyKeyword)))
                    });
                }

                return node;
            }
        }
    }
}