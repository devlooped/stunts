using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Stunts.CodeAnalysis
{
    /// <summary>
    /// Naming conventions used for analyzers, code fixes and code generation.
    /// </summary>
    public class NamingConvention
    {
        /// <summary>
        /// The root or base namespace of the generated code.
        /// </summary>
        public virtual string RootNamespace => StuntNaming.DefaultRootNamespace;

        /// <summary>
        /// Suffix appended to the type name, i.e. <c>IFooStunt</c>.
        /// </summary>
        public virtual string NameSuffix => StuntNaming.DefaultSuffix;

        /// <summary>
        /// Prefix of the class that holds the default member implementations 
        /// of an interface, i.e. <c>DefaultIFoo</c>.
        /// </summary>
        public virtual string DefaultImplementationPrefix => "Default";

        /// <summary>
        /// The type name to generate for the given (optional) base type and implemented interfaces.
        /// </summary>
        public string GetName(IEnumerable<INamedTypeSymbol> symbols)
        {
            var builder = new StringBuilder();
            AddNames(builder, Sorted(symbols));
            return builder.Append(NameSuffix).ToString();
        }

        /// <summary>
        /// The <see cref="GetName"/> followed by a stable hash of the types, used when 
        /// different types would otherwise get the same name, i.e. <c>IFoo&lt;A.Bar&gt;</c> 
        /// and <c>IFoo&lt;B.Bar&gt;</c>.
        /// </summary>
        public string GetUniqueName(IEnumerable<INamedTypeSymbol> symbols)
            => GetName(symbols) + "_" + Hash(GetIdentity(symbols));

        /// <summary>
        /// The type name of the class that holds the default member implementations 
        /// of the given interface.
        /// </summary>
        public string GetDefaultImplementationName(INamedTypeSymbol symbol)
        {
            var builder = new StringBuilder(DefaultImplementationPrefix);
            AddNames(builder, new[] { symbol });
            return builder.ToString();
        }

        /// <summary>
        /// The <see cref="GetDefaultImplementationName"/> followed by a stable hash of the 
        /// interface, used when different interfaces would otherwise get the same name.
        /// </summary>
        public string GetUniqueDefaultImplementationName(INamedTypeSymbol symbol)
            => GetDefaultImplementationName(symbol) + "_" + Hash(GetIdentity(new[] { symbol }));

        /// <summary>
        /// The full type name of the class that holds the default member implementations 
        /// of the given interface.
        /// </summary>
        public string GetDefaultImplementationFullName(INamedTypeSymbol symbol)
            => GetNamespace(new[] { symbol }) + "." + GetDefaultImplementationName(symbol);

        static void AddNames(StringBuilder builder, IEnumerable<ITypeSymbol> symbols)
        {
            foreach (var symbol in symbols)
                AddName(builder, symbol);
        }

        static void AddName(StringBuilder builder, ITypeSymbol symbol)
        {
            if (symbol is IArrayTypeSymbol array)
            {
                builder.Append("Array");
                if (array.Rank > 1)
                    builder.Append(array.Rank).Append('D');
                builder.Append("Of");
                AddName(builder, array.ElementType);
                return;
            }

            builder.Append(symbol.Name);
            if (symbol is INamedTypeSymbol named && named.IsGenericType)
            {
                builder.Append("Of");
                AddNames(builder, named.TypeArguments);
            }
        }

        /// <summary>
        /// The run-time identity of the given types: two sets of symbols that produce the same 
        /// run-time types (regardless of order, nullable annotations or tuple element names) 
        /// have the same identity.
        /// </summary>
        internal static string GetIdentity(IEnumerable<INamedTypeSymbol> symbols)
        {
            var bases = symbols.Where(x => x.TypeKind == TypeKind.Class || x.TypeKind == TypeKind.Delegate).Select(GetIdentity);
            var interfaces = symbols.Where(x => x.TypeKind == TypeKind.Interface).Select(GetIdentity).OrderBy(x => x, StringComparer.Ordinal);
            return string.Join(", ", bases.Concat(interfaces));
        }

        static string GetIdentity(ITypeSymbol symbol)
        {
            var builder = new StringBuilder();
            AddIdentity(builder, symbol);
            return builder.ToString();
        }

        static void AddIdentity(StringBuilder builder, ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case IArrayTypeSymbol array:
                    AddIdentity(builder, array.ElementType);
                    builder.Append('[').Append(',', array.Rank - 1).Append(']');
                    break;
                case IPointerTypeSymbol pointer:
                    AddIdentity(builder, pointer.PointedAtType);
                    builder.Append('*');
                    break;
                case IDynamicTypeSymbol:
                    builder.Append("System.Object");
                    break;
                case INamedTypeSymbol named:
                    if (named.ContainingType != null)
                        AddIdentity(builder, named.ContainingType);
                    else if (named.ContainingNamespace is { IsGlobalNamespace: false } ns)
                        builder.Append(ns.ToDisplayString());

                    builder.Append(named.ContainingType != null ? '+' : '.').Append(named.MetadataName);
                    if (named.TypeArguments.Length > 0)
                    {
                        builder.Append('[');
                        for (var i = 0; i < named.TypeArguments.Length; i++)
                        {
                            if (i > 0)
                                builder.Append(',');
                            AddIdentity(builder, named.TypeArguments[i]);
                        }
                        builder.Append(']');
                    }
                    break;
                default:
                    builder.Append(symbol.ToDisplayString());
                    break;
            }
        }

        // FNV-1a: string.GetHashCode is randomized per process, and names must be stable across builds.
        static string Hash(string value)
        {
            var hash = 2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }

        /// <summary>
        /// The full type name for the given (optional) base type and implemented interfaces.
        /// </summary>
        public string GetFullName(IEnumerable<INamedTypeSymbol> symbols)
            => GetNamespace(RootNamespace, symbols.FirstOrDefault()?.ContainingNamespace) + "." + GetName(symbols);

        /// <summary>
        /// The namespace for the given (optional) base type and implemented interfaces.
        /// </summary>
        public string GetNamespace(IEnumerable<INamedTypeSymbol> symbols)
            => GetNamespace(RootNamespace, symbols.FirstOrDefault()?.ContainingNamespace);

        string GetNamespace(string rootNamespace, INamespaceSymbol? containingNamespace)
            => containingNamespace == null || containingNamespace.IsGlobalNamespace ? rootNamespace : rootNamespace + "." + containingNamespace.ToString();

        IEnumerable<INamedTypeSymbol> Sorted(IEnumerable<INamedTypeSymbol> symbols)
            => symbols.Where(x => x.TypeKind == TypeKind.Class || x.TypeKind == TypeKind.Delegate)
                .Concat(symbols.Where(x => x.TypeKind == TypeKind.Interface).OrderBy(x => x.Name, StringComparer.Ordinal));
    }
}
