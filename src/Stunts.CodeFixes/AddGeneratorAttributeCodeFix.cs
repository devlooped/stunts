using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Adds the generator attribute required by <see cref="StuntDiagnostics.UnannotatedGenericWrapper"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddGeneratorAttributeCodeFix))]
    [Shared]
    public class AddGeneratorAttributeCodeFix : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(StuntDiagnostics.UnannotatedGenericWrapper.Id);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics[0];
            if (diagnostic.AdditionalLocations.FirstOrDefault() is not Location location ||
                location.SourceTree == null ||
                !diagnostic.Properties.TryGetValue(GenericWrapperAnalyzer.AttributeProperty, out var attribute) ||
                attribute == null)
                return;

            var document = context.Document.Project.Solution.GetDocument(location.SourceTree);
            if (document == null)
                return;

            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var method = root?.FindToken(location.SourceSpan.Start).Parent?.AncestorsAndSelf()
                .FirstOrDefault(node => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax);
            if (method == null)
                return;

            var name = attribute.EndsWith("Attribute") ? attribute.Substring(0, attribute.Length - "Attribute".Length) : attribute;
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Add [{name.Split('.').Last()}]",
                    cancellationToken => AddAttributeAsync(document, method, name, cancellationToken),
                    nameof(AddGeneratorAttributeCodeFix)),
                diagnostic);
        }

        static async Task<Solution> AddAttributeAsync(Document document, SyntaxNode method, string name, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document.Project.Solution;

            var leading = method.GetLeadingTrivia();
            var indentation = leading.LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));
            var list = AttributeList(SingletonSeparatedList(
                Attribute(ParseName("global::" + name).WithAdditionalAnnotations(Simplifier.Annotation))))
                .WithLeadingTrivia(leading)
                .WithTrailingTrivia(indentation == default
                    ? TriviaList(ElasticCarriageReturnLineFeed)
                    : TriviaList(ElasticCarriageReturnLineFeed, indentation));

            SyntaxNode updated = method switch
            {
                MethodDeclarationSyntax declaration => declaration.WithoutLeadingTrivia() is var bare
                    ? bare.WithAttributeLists(bare.AttributeLists.Insert(0, list)) : method,
                LocalFunctionStatementSyntax local => local.WithoutLeadingTrivia() is var bareLocal
                    ? bareLocal.WithAttributeLists(bareLocal.AttributeLists.Insert(0, list)) : method,
                _ => method,
            };

            return document.WithSyntaxRoot(root.ReplaceNode(method, updated)).Project.Solution;
        }
    }
}
