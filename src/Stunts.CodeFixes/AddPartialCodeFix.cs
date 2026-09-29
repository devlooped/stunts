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
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Adds the <c>partial</c> modifier required by <see cref="StuntDiagnostics.ContainingTypeNotPartial"/>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddPartialCodeFix))]
    [Shared]
    public class AddPartialCodeFix : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(StuntDiagnostics.ContainingTypeNotPartial.Id);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (model == null)
                return;

            IInvocationOperation? invocation = null;
            for (SyntaxNode? current = node; current != null && invocation == null; current = current.Parent)
                invocation = model.GetOperation(current, context.CancellationToken) as IInvocationOperation;

            var type = invocation?.TargetMethod.TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault(NestedTypeStunt.MustNest);
            var container = type == null ? null : NestedTypeStunt.NonPartialContainer(type);
            if (container == null || container.DeclaringSyntaxReferences.IsEmpty)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Make '{container.Name}' partial",
                    cancellationToken => AddPartialAsync(context.Document.Project.Solution, container, cancellationToken),
                    nameof(AddPartialCodeFix)),
                diagnostic);
        }

        static async Task<Solution> AddPartialAsync(Solution solution, INamedTypeSymbol container, CancellationToken cancellationToken)
        {
            foreach (var reference in container.DeclaringSyntaxReferences)
            {
                if (await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax declaration)
                    continue;
                if (NestedTypeStunt.HasPartialModifier(declaration))
                    continue;

                var document = solution.GetDocument(reference.SyntaxTree);
                if (document == null)
                    continue;

                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                if (root == null)
                    continue;

                var updated = declaration.WithModifiers(declaration.Modifiers.Add(Token(SyntaxKind.PartialKeyword)));
                solution = document.WithSyntaxRoot(root.ReplaceNode(declaration, updated)).Project.Solution;
            }

            return solution;
        }
    }
}
