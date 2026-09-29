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
        /// The full type name of the class that holds the default member implementations 
        /// of the given interface.
        /// </summary>
        public string GetDefaultImplementationFullName(INamedTypeSymbol symbol)
            => GetNamespace(new[] { symbol }) + "." + GetDefaultImplementationName(symbol);

        static void AddNames(StringBuilder builder, IEnumerable<ITypeSymbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                builder.Append(symbol.Name);
                if (symbol is INamedTypeSymbol named && named.IsGenericType)
                {
                    builder.Append("Of");
                    AddNames(builder, named.TypeArguments);
                }
            }
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
            => symbols.Where(x => x.TypeKind == TypeKind.Class)
                .Concat(symbols.Where(x => x.TypeKind == TypeKind.Interface).OrderBy(x => x.Name));
    }
}
