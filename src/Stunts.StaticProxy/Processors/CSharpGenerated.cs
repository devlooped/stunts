using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.Processors
{
    /// <summary>
    /// Adds a <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/> 
    /// attribute to all generated members, so that it's possible to distinguish user-authored 
    /// members in a partial class from the generated code.
    /// </summary>
    public class CSharpGenerated : ISyntaxProcessor
    {
        /// <summary>
        /// Applies to <see cref="LanguageNames.CSharp"/> only.
        /// </summary>
        public string Language { get; } = LanguageNames.CSharp;

        /// <summary>
        /// Runs in the final phase of codegen, <see cref="ProcessorPhase.Fixup"/>.
        /// </summary>
        public ProcessorPhase Phase => ProcessorPhase.Fixup;

        /// <summary>
        /// Applies the attribute to all members in the document.
        /// </summary>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
            => new CSharpRewriteVisitor().Visit(syntax);

        class CSharpRewriteVisitor : CSharpSyntaxRewriter
        {
            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitClassDeclaration(node);

                return base.VisitClassDeclaration(AddAttributes(node));
            }

            public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitConstructorDeclaration(node);

                return base.VisitConstructorDeclaration(AddAttributes(node));
            }

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitMethodDeclaration(node);

                return base.VisitMethodDeclaration(AddAttributes(node));
            }

            public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitPropertyDeclaration(node);

                return base.VisitPropertyDeclaration(AddAttributes(node));
            }

            public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitIndexerDeclaration(node);

                return base.VisitIndexerDeclaration(AddAttributes(node));
            }

            public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
            {
                if (node.AttributeLists.HasAttribute("CompilerGenerated"))
                    return base.VisitEventDeclaration(node);

                return base.VisitEventDeclaration(AddAttributes(node));
            }

            TSyntax AddAttributes<TSyntax>(TSyntax node) where TSyntax : MemberDeclarationSyntax
                => (TSyntax)node.WithAttributeLists(
                    List(
                        node.AttributeLists.Concat(new[]
                        {
                            AttributeList(
                                SingletonSeparatedList(
                                    Attribute(IdentifierName("CompilerGenerated"))
                            ))
                        })));
        }
    }
}