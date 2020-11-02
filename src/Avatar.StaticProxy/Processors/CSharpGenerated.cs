﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Avatars.Processors
{
    /// <summary>
    /// Adds a <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/> 
    /// attribute to all generated members, so that it's possible to distinguish user-authored 
    /// members in a partial class from the generated code.
    /// </summary>
    public class CSharpGenerated : IDocumentProcessor
    {
        /// <summary>
        /// Applies to <see cref="LanguageNames.CSharp"/> only.
        /// </summary>
        public string[] Languages { get; } = new[] { LanguageNames.CSharp };

        /// <summary>
        /// Runs in the final phase of codegen, <see cref="ProcessorPhase.Fixup"/>.
        /// </summary>
        public ProcessorPhase Phase => ProcessorPhase.Fixup;

        /// <summary>
        /// Applies the attribute to all members in the document.
        /// </summary>
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            var syntax = await document.GetSyntaxRootAsync(cancellationToken);
            if (syntax == null)
                return document;

            syntax = new CSharpRewriteVisitor(SyntaxGenerator.GetGenerator(document)).Visit(syntax);

            return document.WithSyntaxRoot(syntax);
        }

        class CSharpRewriteVisitor : CSharpSyntaxRewriter
        {
            readonly SyntaxGenerator generator;

            public CSharpRewriteVisitor(SyntaxGenerator generator) => this.generator = generator;

            public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                if (generator.GetAttributes(node).Any(attr => generator.GetName(attr) == "CompilerGenerated"))
                    return base.VisitClassDeclaration(node);

                return base.VisitClassDeclaration((ClassDeclarationSyntax)AddAttributes(node));
            }

            SyntaxNode AddAttributes(SyntaxNode node)
                => generator.AddAttributes(node,
                    Attribute(IdentifierName("CompilerGenerated")));
        }
    }
}