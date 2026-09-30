using Microsoft.CodeAnalysis;
using static ThisAssembly;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Known diagnostics reported by the Stunts analyzer.
    /// </summary>
    public static class StuntDiagnostics
    {
        /// <summary>
        /// Diagnostic reported whenever type parameters specified for a 
        /// <see cref="StuntGeneratorAttribute"/>-annotated method contain a base 
        /// type but it is not the first provided type parameter. This matches 
        /// the compiler requirement of having the base class as the first type too.
        /// </summary>
        public static DiagnosticDescriptor BaseTypeNotFirst { get; } = new DiagnosticDescriptor(
            "ST001",
            nameof(Strings.BaseTypeNotFirst.Title),
            Resources.BaseTypeNotFirst_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.BaseTypeNotFirst.Description);

        /// <summary>
        /// Diagnostic reported whenever the base type specified for a 
        /// <see cref="StuntGeneratorAttribute"/>-annotated method is duplicated.
        /// </summary>
        public static DiagnosticDescriptor DuplicateBaseType { get; } = new DiagnosticDescriptor(
            "ST002",
            Strings.DuplicateBaseType.Title,
            Resources.DuplicateBaseType_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.DuplicateBaseType.Description);

        /// <summary>
        /// Diagnostic reported whenever the specified base type for a 
        /// <see cref="StuntGeneratorAttribute"/>-annotated method is sealed.
        /// </summary>
        public static DiagnosticDescriptor SealedBaseType { get; } = new DiagnosticDescriptor(
            "ST003",
            Strings.SealedBaseType.Title,
            Resources.SealedBaseType_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.SealedBaseType.Description);

        /// <summary>
        /// Diagnostic reported whenever the specified base type for a 
        /// <see cref="StuntGeneratorAttribute"/>-annotated method is an enum.
        /// </summary>
        public static DiagnosticDescriptor EnumType { get; } = new DiagnosticDescriptor(
            "ST004",
            Strings.EnumType.Title,
            Resources.EnumType_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.EnumType.Description);

        /// <summary>
        /// Diagnostic reported when type used contains at least one member that uses 
        /// pointer type arguments, which are unsupported at the moment.
        /// </summary>
        public static DiagnosticDescriptor PointerMember { get; } = new DiagnosticDescriptor(
            "ST005",
            Strings.PointerMember.Title,
            Resources.PointerMember_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.PointerMember.Description);

        /// <summary>
        /// Diagnostic reported whenever the specified base type for a 
        /// <see cref="StuntGeneratorAttribute"/>-annotated method is sealed.
        /// </summary>
        public static DiagnosticDescriptor BaseTypeNoContructor { get; } = new DiagnosticDescriptor(
            "ST006",
            Strings.BaseTypeNoCtor.Title,
            Resources.BaseTypeNoCtor_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.BaseTypeNoCtor.Description);

        /// <summary>
        /// Diagnostic reported whenever the specified generator attribute to use to flag 
        /// stunt-generating invocations cannot be located in the current compilation for some 
        /// reason.
        /// </summary>
        /// <summary>
        /// Diagnostic reported when a nested type can only be inherited from inside its 
        /// containing type, and that type is not partial in this compilation.
        /// </summary>
        /// <summary>
        /// Diagnostic reported when a delegate type argument is combined with any other type.
        /// A delegate stunt is only <c>Stunt.Of&lt;SomeDelegate&gt;()</c>.
        /// </summary>
        public static DiagnosticDescriptor DelegateWithOtherTypes { get; } = new DiagnosticDescriptor(
            "ST010",
            "Delegate type combined with other types",
            "Delegate type '{0}' must be the only type argument",
            "Build",
            DiagnosticSeverity.Error,
            true,
            "A delegate stunt is created with Stunt.Of<T>() where T is the delegate. It cannot be combined with a class or additional interfaces.");

        public static DiagnosticDescriptor ContainingTypeNotPartial { get; } = new DiagnosticDescriptor(
            "ST008",
            "Containing type is not partial",
            "'{0}' can only be inherited from inside '{1}', which must be partial",
            "Build",
            DiagnosticSeverity.Error,
            true,
            "Private, protected, and private protected nested types are proxied by a stunt nested in the containing type. That type has to be declared partial.");

        /// <summary>
        /// Diagnostic reported when a stunt signature uses a ref struct or pointer
        /// and compile-time stunts or unsafe blocks are not enabled.
        /// </summary>
        public static DiagnosticDescriptor UnsafeSignature { get; } = new DiagnosticDescriptor(
            "ST009",
            "Ref structs and pointers require compile-time stunts",
            "'{0}.{1}' uses a ref struct or pointer and requires compile-time stunts and AllowUnsafeBlocks",
            "Build",
            DiagnosticSeverity.Error,
            true,
            "Proxying ref structs and pointers generates unsafe code. EnableCompileTimeStunts and AllowUnsafeBlocks must both be true, or that code does not compile.");

        public static DiagnosticDescriptor GeneratorAttributeNotFound { get; } = new DiagnosticDescriptor(
            "ST007",
            Strings.GeneratorAttributeNotFound.Title,
            Resources.GeneratorAttributeNotFound_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.GeneratorAttributeNotFound.Description);
    }
}
