using Microsoft.CodeAnalysis;

namespace Stunts.Processors
{
    /// <summary>
    /// Annotations the scaffold leaves on syntax nodes for a later rewrite pass.
    /// </summary>
    static class Annotations
    {
        /// <summary>A <c>ref struct</c> parameter or return.</summary>
        public static readonly SyntaxAnnotation StructRef = new("Stunts.StructRef");

        /// <summary>A pointer or function-pointer parameter or return.</summary>
        public static readonly SyntaxAnnotation PointerRef = new("Stunts.PointerRef");

        /// <summary>A call that proceeds to a default interface implementation.</summary>
        public static readonly SyntaxAnnotation DefaultImplementation = new("Stunts.DefaultImplementation");

        /// <summary>The type an invocation target is cast to when it is not the stunt.</summary>
        public const string ForwardKind = "Stunts.Forward";

        /// <summary>Marks the type name stored for <see cref="ForwardKind"/>.</summary>
        public static SyntaxAnnotation Forward(string typeName) => new(ForwardKind, typeName);
    }
}
