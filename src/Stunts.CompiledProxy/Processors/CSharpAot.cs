using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts.Processors
{
    /// <summary>Roots closed stunts and emits statically typed default value factories.</summary>
    public class CSharpAot : ISyntaxProcessor
    {
        /// <inheritdoc/>
        public string Language => LanguageNames.CSharp;

        /// <inheritdoc/>
        public ProcessorPhase Phase => ProcessorPhase.Fixup;

        /// <inheritdoc/>
        public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
        {
            if (syntax is not CompilationUnitSyntax unit)
                return syntax;

            var declaration = unit.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault(type => type.BaseList != null);
            if (declaration == null)
                return syntax;

            var suppression = context.Compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute");
            var canAnnotate = suppression != null && context.Compilation.IsSymbolAccessibleWithin(suppression, context.Compilation.Assembly);
            var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                return syntax;

            var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var asyncAdapters = new HashSet<string>();
            var registrations = new List<StatementSyntax>();
            var usesQueryable = false;
            void RegisterAwaitable(ITypeSymbol type)
            {
                if (type is not INamedTypeSymbol named || !named.IsGenericType)
                    return;

                var definition = named.OriginalDefinition.ToDisplayString();
                if (definition != "System.Threading.Tasks.Task<TResult>" &&
                    definition != "System.Threading.Tasks.ValueTask<TResult>")
                    return;

                var argument = named.TypeArguments[0];
                if (argument.TypeKind is TypeKind.TypeParameter or TypeKind.Error || argument.IsRefLikeType)
                    return;

                var name = argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (asyncAdapters.Add(name))
                    registrations.Add(ParseStatement($"global::Stunts.AsyncRegistry.Register<{name}>();"));
            }
            void Register(ITypeSymbol type)
            {
                if (type.SpecialType == SpecialType.System_Void || type.IsRefLikeType ||
                    type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.TypeParameter or TypeKind.Error ||
                    !types.Add(type))
                    return;

                var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var expression = $"default({name})";
                var fallback = expression;
                if (type is IArrayTypeSymbol array)
                {
                    Register(array.ElementType);
                    expression = EmptyArray(array);
                }
                else if (type is INamedTypeSymbol named)
                {
                    foreach (var argument in named.TypeArguments)
                        Register(argument);
                    var definition = named.OriginalDefinition.ToDisplayString();
                    var arguments = named.TypeArguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToArray();
                    if (named.SpecialType == SpecialType.System_Array)
                        expression = "throw new global::System.ArgumentException(\"type\")";
                    else if (definition == "System.Threading.Tasks.Task<TResult>")
                        expression = $"global::System.Threading.Tasks.Task.FromResult(provider.GetDefault<{arguments[0]}>())";
                    else if (definition == "System.Threading.Tasks.ValueTask<TResult>")
                        expression = $"new {name}(provider.GetDefault<{arguments[0]}>())";
                    else if (definition == "System.Collections.Generic.IEnumerable<T>")
                        expression = EmptyArray(context.Compilation.CreateArrayTypeSymbol(named.TypeArguments[0]));
                    else if (named.Name == "ValueTuple" && named.ContainingNamespace.ToDisplayString() == "System" && arguments.Length > 0)
                        expression = $"new global::System.ValueTuple<{string.Join(", ", arguments)}>({string.Join(", ", arguments.Select(argument => $"provider.GetDefault<{argument}>()"))})";
                    else if (named.ContainingNamespace.ToDisplayString() == "System.Linq" && named.Name == "IQueryable")
                    {
                        usesQueryable = true;
                        expression = "throw new global::System.NotSupportedException(\"Queryable defaults require dynamic code. Register a typed DefaultValueProvider factory for Native AOT.\")";
                    }
                    else if (named.IsValueType && named.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
                        expression = $"new {name}()";
                    if (named.IsValueType && !named.IsTupleType && named.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
                        fallback = $"new {name}()";
                }
                registrations.Add(ParseStatement($"global::Stunts.DefaultValueProvider.RegisterGenerated<{name}>(provider => {expression}, () => {fallback});"));
            }

            foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>().Where(method => !method.IsGenericMethod && !method.IsStatic))
            {
                Register(method.ReturnType);
                RegisterAwaitable(method.ReturnType);
                foreach (var parameter in method.Parameters.Where(parameter => parameter.RefKind == RefKind.Out))
                    Register(parameter.Type);
            }

            var updated = canAnnotate
                ? (TypeDeclarationSyntax)new RootedMetadata().Visit(declaration)! : declaration;
            if (registrations.Count > 0)
                updated = updated.AddMembers(ConstructorDeclaration(declaration.Identifier)
                    .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
                    .WithBody(Block(registrations)));
            if (usesQueryable)
                updated = updated.WithAdditionalAnnotations(new SyntaxAnnotation("Stunts.AotQueryable"));
            if (!context.Compilation.SupportsRuntimeCapability(RuntimeCapability.ByRefLikeGenerics) &&
                symbol.GetMembers().OfType<IMethodSymbol>().Any(method => method.ReturnType.IsRefLikeType || method.Parameters.Any(parameter => parameter.Type.IsRefLikeType)))
                updated = updated.WithAdditionalAnnotations(new SyntaxAnnotation("Stunts.AotRefStruct"));
            return unit.ReplaceNode(declaration, updated);
        }

        static string EmptyArray(IArrayTypeSymbol array)
        {
            var syntax = (ArrayTypeSyntax)ParseTypeName(array.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            var rank = syntax.RankSpecifiers[0].WithSizes(SeparatedList<ExpressionSyntax>(
                Enumerable.Repeat<ExpressionSyntax>(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)), array.Rank)));
            return ArrayCreationExpression(syntax.WithRankSpecifiers(syntax.RankSpecifiers.Replace(syntax.RankSpecifiers[0], rank))).NormalizeWhitespace().ToFullString();
        }

        sealed class RootedMetadata : CSharpSyntaxRewriter
        {
            static readonly AttributeListSyntax metadata = ParseCompilationUnit(
                "[global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"CompiledStuntFactory.Register preserves all members of registered stunts.\")] class Placeholder {}")
                .Members.OfType<ClassDeclarationSyntax>().Single().AttributeLists.Single();

            static SyntaxList<AttributeListSyntax> Root(SyntaxList<AttributeListSyntax> attributes) => attributes.Add(metadata);

            public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
                => node.TypeParameterList == null ? node.WithAttributeLists(Root(node.AttributeLists)) : node;

            public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
                => node.WithAttributeLists(Root(node.AttributeLists));

            public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
                => node.WithAttributeLists(Root(node.AttributeLists));

            public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (node.ExpressionBody != null)
                    return node.WithExpressionBody(null).WithSemicolonToken(default)
                        .WithAccessorList(AccessorList(SingletonList(
                            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithAttributeLists(Root(default))
                                .WithExpressionBody(node.ExpressionBody)
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));
                return base.VisitPropertyDeclaration(node);
            }

            public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
            {
                if (node.ExpressionBody != null)
                    return node.WithExpressionBody(null).WithSemicolonToken(default)
                        .WithAccessorList(AccessorList(SingletonList(
                            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithAttributeLists(Root(default))
                                .WithExpressionBody(node.ExpressionBody)
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));
                return base.VisitIndexerDeclaration(node);
            }
        }
    }
}
