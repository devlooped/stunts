using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stunts.CodeAnalysis;
using Stunts.Processors;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Stunts
{
    /// <summary>
    /// The class that holds the default member implementations of an interface, i.e.:
    /// <code>
    /// public class DefaultIFoo : IFoo
    /// {
    ///     public static IFoo Default { get; } = new DefaultIFoo();
    ///     public void Do() => throw new NotImplementedException();
    /// }
    /// </code>
    /// Members with a default implementation are left unimplemented, so invoking them 
    /// through <c>Default</c> runs the interface's default. Stunts proceed to it when 
    /// no behavior short-circuits the call.
    /// </summary>
    static class DefaultImplementation
    {
        /// <summary>
        /// Name of the static property that exposes the instance typed as the interface.
        /// </summary>
        public const string InstanceName = "Default";

        public static SyntaxProcessorDriver Driver { get; } = new SyntaxProcessorDriver(
            new DefaultImports(typeof(CompilerGeneratedAttribute).Namespace!),
            new Scaffold(),
            new CSharpGenerated(),
            new FixupImports(),
            new CSharpFileHeader(),
            new CSharpPragmas());

        public static SyntaxNode CreateSyntax(NamingConvention naming, INamedTypeSymbol iface)
            => CompilationUnit()
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(ParseName(naming.GetNamespace(new[] { iface })))
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(
                                ClassDeclaration(naming.GetDefaultImplementationName(iface))
                                .WithModifiers(TokenList(Token(IsPublic(iface) ? SyntaxKind.PublicKeyword : SyntaxKind.InternalKeyword)))
                                .WithBaseList(
                                    BaseList(
                                        SingletonSeparatedList<BaseTypeSyntax>(
                                            SimpleBaseType(MemberScaffold.TypeName(iface)))))))));

        // A public class implementing a less accessible interface does not compile (CS0060).
        static bool IsPublic(ITypeSymbol type) => type switch
        {
            IArrayTypeSymbol array => IsPublic(array.ElementType),
            INamedTypeSymbol named => named.DeclaredAccessibility == Accessibility.Public &&
                (named.ContainingType == null || IsPublic(named.ContainingType)) &&
                named.TypeArguments.All(IsPublic),
            _ => true,
        };

        class Scaffold : ISyntaxProcessor
        {
            public string Language => LanguageNames.CSharp;

            public ProcessorPhase Phase => ProcessorPhase.Scaffold;

            public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
            {
                var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                var declaration = syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (declaration == null ||
                    model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol symbol ||
                    symbol.Interfaces.Length != 1)
                    return syntax;

                // public static IFoo Default { get; } = new DefaultIFoo();
                var instance = PropertyDeclaration(MemberScaffold.TypeName(symbol.Interfaces[0]), InstanceName)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
                    .WithAccessorList(AccessorList(SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))))
                    .WithInitializer(EqualsValueClause(
                        ObjectCreationExpression(IdentifierName(symbol.Name)).WithArgumentList(ArgumentList())))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                var members = new List<MemberDeclarationSyntax> { instance };
                members.AddRange(MemberScaffold.InterfaceStubs(symbol, new Dictionary<string, ISymbol>(), context.CancellationToken));

                return syntax.ReplaceNode(declaration, declaration.AddMembers(members.ToArray()));
            }
        }
    }
}
