using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// A private, protected, or private protected nested type can only be inherited
    /// from inside its containing type, so the stunt is nested there. That requires
    /// every enclosing type to be partial.
    /// </summary>
    public static class NestedTypeStunt
    {
        /// <summary>
        /// Whether a stunt for <paramref name="type"/> has to be nested in its containing type.
        /// </summary>
        public static bool MustNest(INamedTypeSymbol type)
            => type.ContainingType != null && type.DeclaredAccessibility is
                Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal;

        /// <summary>
        /// The nearest enclosing type that is not declared partial, when <paramref name="type"/>
        /// must be nested to be inherited.
        /// </summary>
        public static INamedTypeSymbol? NonPartialContainer(INamedTypeSymbol type)
        {
            if (!MustNest(type))
                return null;

            for (var current = type.ContainingType; current != null; current = current.ContainingType)
            {
                if (!IsPartial(current))
                    return current;
            }

            return null;
        }

        /// <summary>
        /// Whether any declaration of <paramref name="type"/> has the partial modifier.
        /// </summary>
        public static bool IsPartial(INamedTypeSymbol type)
            => type.DeclaringSyntaxReferences.Any(reference => HasPartialModifier(reference.GetSyntax()));

        /// <summary>
        /// Whether <paramref name="declaration"/> includes a partial modifier.
        /// </summary>
        public static bool HasPartialModifier(SyntaxNode declaration)
            => declaration.ChildTokens().Any(token =>
                token.ValueText.Equals("partial", StringComparison.OrdinalIgnoreCase));
    }
}
