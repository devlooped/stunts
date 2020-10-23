﻿using Microsoft.CodeAnalysis;
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
        /// <see cref="StuntGeneratorAttribute"/>-annotated method sealed.
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
        /// Diagnostic reported when type used is nested, which is unsupported at the moment.
        /// </summary>
        public static DiagnosticDescriptor NestedType { get; } = new DiagnosticDescriptor(
            "ST004",
            Strings.NestedType.Title,
            Resources.NestedType_Message,
            "Build",
            DiagnosticSeverity.Error,
            true,
            Strings.NestedType.Description);

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
    }
}
