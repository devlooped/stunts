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
            int genericDepth;

            public override SyntaxNode? VisitTypeOfExpression(TypeOfExpressionSyntax node)
            {
                var runtime = node.Type.GetAnnotations("Stunts.RuntimeType").FirstOrDefault()?.Data;
                return runtime == null ? base.VisitTypeOfExpression(node) : node.WithType(ParseTypeName(runtime));
            }

            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                if (node.TypeParameterList != null)
                    genericDepth++;
                node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
                var isGeneric = genericDepth > 0;
                if (node.TypeParameterList != null)
                    genericDepth--;
                // Enclosing partials exist only so the stunt can inherit a private nested type.
                return node.BaseList == null ? node : Finish(node, isGeneric);
            }

            public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                if (node.TypeParameterList != null)
                    genericDepth++;
                node = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
                var isGeneric = genericDepth > 0;
                if (node.TypeParameterList != null)
                    genericDepth--;
                return node.BaseList == null ? node : Finish(node, isGeneric);
            }

            static TDeclaration Finish<TDeclaration>(TDeclaration node, bool isGeneric)
                where TDeclaration : TypeDeclarationSyntax
            {
                TypeSyntax self = node.TypeParameterList == null
                    ? IdentifierName(node.Identifier.ValueText)
                    : GenericName(node.Identifier).WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(
                        node.TypeParameterList.Parameters.Select(parameter => IdentifierName(parameter.Identifier)))));
                if (isGeneric)
                    node = (TDeclaration)new ConstructedMethods(self).Visit(node)!;

                if (node.BaseList != null && !node.BaseList.Types.Any(x =>
                    x.ToString() == nameof(IStunt) ||
                    x.ToString() == typeof(IStunt).FullName))
                {
                    // Only add the base type if it isn't already there
                    node = (TDeclaration)node.AddBaseListTypes(SimpleBaseType(IdentifierName(nameof(IStunt))));
                }

                if (!node.Members.OfType<PropertyDeclarationSyntax>().Any(prop =>
                    prop.Identifier.ValueText == nameof(IStunt.Behaviors) && prop.ExplicitInterfaceSpecifier?.Name.ToString() == nameof(IStunt)))
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
                        node = (TDeclaration)node.InsertNodesAfter(node.Members.First(), new[] { behaviors });
                    else
                        node = (TDeclaration)node.AddMembers(behaviors);
                }

                if (!node.Members.OfType<FieldDeclarationSyntax>().Any(x => x.Declaration.Variables.Any(v => v.Identifier.ToString() == "pipeline")))
                {
                    node = (TDeclaration)node.InsertNodesBefore(node.Members.First(), new[]
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
                                            self))))
                            .NormalizeWhitespace()
                        ).WithModifiers(TokenList(Token(SyntaxKind.ReadOnlyKeyword)))
                    });
                }

                return node;
            }

            sealed class ConstructedMethods : CSharpSyntaxRewriter
            {
                readonly TypeSyntax self;

                public ConstructedMethods(TypeSyntax self) => this.self = self;

                public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
                {
                    if (node.Expression is not MemberAccessExpressionSyntax member ||
                        member.Expression.ToString() != "MethodBase" || member.Name.Identifier.ValueText != "GetCurrentMethod")
                        return base.VisitInvocationExpression(node);

                    return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("MethodBase"), IdentifierName("GetMethodFromHandle")),
                        ArgumentList(SeparatedList(new[]
                        {
                            Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                node, IdentifierName("MethodHandle"))),
                            Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                TypeOfExpression(self), IdentifierName("TypeHandle"))),
                        })));
                }
            }
        }
    }
}