using Microsoft.CodeAnalysis;
using static ThisAssembly;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Known diagnostics reported by the Stunts analyzer.
    /// </summary>
    public static class StuntDiagnostics
    {
        /// <summary>Reports intercepted members that require runtime generic instantiation.</summary>
        public static DiagnosticDescriptor AotUnsupportedMember { get; } = new DiagnosticDescriptor(
            "ST015", "Stunt member requires dynamic code",
            "This stunt has a Native AOT limitation: {0}",
            "Build", DiagnosticSeverity.Warning, true);

        /// <summary>
        /// Reports a generic method that passes its own type parameters to a generator 
        /// method without being a generator method itself, so the stunt types its 
        /// callers need cannot be generated.
        /// </summary>
        public static DiagnosticDescriptor UnannotatedGenericWrapper { get; } = new DiagnosticDescriptor(
            "ST016", "Generic stunt wrapper must be a generator method",
            "'{0}' passes its type parameters to '{1}', so it must be annotated with [{2}] for its callers to get compile-time stunts",
            "Build", DiagnosticSeverity.Error, true);

        /// <summary>
        /// Reports a generator invocation that uses type parameters of a containing type, 
        /// which cannot be closed at call sites.
        /// </summary>
        public static DiagnosticDescriptor ContainingTypeParameter { get; } = new DiagnosticDescriptor(
            "ST017", "Stunt types cannot use type parameters of a containing type",
            "'{0}' uses type parameters of its containing type '{1}' to create stunts. Pass them as type parameters of a generic method annotated with [{2}] instead.",
            "Build", DiagnosticSeverity.Error, true);

        /// <summary>
        /// Reports a virtual generic wrapper, whose actual implementation (and stunt types) 
        /// cannot be known at its call sites.
        /// </summary>
        public static DiagnosticDescriptor VirtualGenericWrapper { get; } = new DiagnosticDescriptor(
            "ST018", "Generic stunt wrapper cannot be virtual",
            "'{0}' passes its type parameters to other generator methods, so it cannot be virtual, abstract, an override or an interface member",
            "Build", DiagnosticSeverity.Error, true);

        /// <summary>
        /// Reports a generic wrapper whose stunt types cannot be determined by following 
        /// the wrapper chain.
        /// </summary>
        public static DiagnosticDescriptor UnboundedGenericWrapper { get; } = new DiagnosticDescriptor(
            "ST019", "Generic stunt wrapper cannot be closed",
            "The stunt types created by '{0}' cannot be determined: {1}",
            "Build", DiagnosticSeverity.Error, true);

        /// <summary>Reports a C# language version that cannot register compile-time stunts.</summary>
        public static DiagnosticDescriptor LanguageVersionNotSupported { get; } = new DiagnosticDescriptor(
            "ST020", "Compile-time stunts require C# 9 or later",
            "Compile-time stunts require C# 9 or later to register themselves. Set LangVersion to 9 or later, or use Stunts.DynamicProxy.",
            "Build", DiagnosticSeverity.Error, true);

        /// <summary>Reports a stunt for a nested type that can only be generated inside a generic type.</summary>
        public static DiagnosticDescriptor GenericContainingType { get; } = new DiagnosticDescriptor(
            "ST021", "Stunts cannot be nested in generic types",
            "A stunt for '{0}' must be nested in the generic type '{1}', which is not supported. Make '{0}' internal or public.",
            "Build", DiagnosticSeverity.Error, true);

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

        /// <summary>
        /// Diagnostic reported when an abstract interface member cannot be implemented
        /// from the assembly containing the stunt.
        /// </summary>
        public static DiagnosticDescriptor InaccessibleInterfaceMember { get; } = new DiagnosticDescriptor(
            "ST012",
            "Interface member is inaccessible",
            "'{0}' cannot be proxied because abstract interface member '{1}' is inaccessible to this assembly",
            "Build",
            DiagnosticSeverity.Error,
            true,
            "All abstract interface members, including property and event accessors, must be accessible to the generated implementation.");

        /// <summary>
        /// Diagnostic reported when a signature uses a special CLR type that cannot
        /// be stored in invocation arguments or ref-struct holders.
        /// </summary>
        public static DiagnosticDescriptor UnsupportedRuntimeSignature { get; } = new DiagnosticDescriptor(
            "ST013",
            "Unsupported CLR signature",
            "'{0}' cannot be proxied because '{1}' uses TypedReference, ArgIterator, or RuntimeArgumentHandle",
            "Build",
            DiagnosticSeverity.Error,
            true,
            "These special CLR types cannot be boxed or used as generic arguments, so the behavior pipeline cannot intercept them.");
    }
}
