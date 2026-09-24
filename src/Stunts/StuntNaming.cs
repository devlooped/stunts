using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stunts
{
    /// <summary>
    /// Exposes the naming conventions for stunt generation and instantiation.
    /// </summary>
    public static class StuntNaming
    {
        /// <summary>
        /// The default root or base namespace where generated stunts are declared.
        /// </summary>
        public const string DefaultRootNamespace = "Stunts";

        /// <summary>
        /// The default suffix added to stunt type names.
        /// </summary>
        public const string DefaultSuffix = "Stunt";

        /// <summary>
        /// Gets the runtime stunt name from its base type and optional additional 
        /// interfaces, using the <see cref="DefaultSuffix"/>.
        /// </summary>
        public static string GetName(Type baseType, params Type[] additionalInterfaces)
            => GetName(DefaultSuffix, baseType, additionalInterfaces);

        /// <summary>
        /// Gets the runtime stunt name from its base type and optional additional interfaces 
        /// and the given <paramref name="suffix"/> appended to the type name.
        /// </summary>
        public static string GetName(string suffix, Type baseType, params Type[] additionalInterfaces)
        {
            if (baseType.IsClass)
            {
                return new StringBuilder()
                    .AddName(baseType)
                    .AddNames(additionalInterfaces.OrderBy(x => x.Name, StringComparer.Ordinal))
                    .Append(suffix)
                    .ToString();
            }
            else
            {
                return new StringBuilder()
                    .AddNames(new[] { baseType }
                        .Concat(additionalInterfaces)
                        .OrderBy(x => x.Name, StringComparer.Ordinal))
                    .Append(suffix)
                    .ToString();
            }
        }

        /// <summary>
        /// Gets the runtime stunt full name from its base type and optional additional interfaces,
        /// using the <see cref="DefaultRootNamespace"/> and <see cref="DefaultSuffix"/>.
        /// </summary>
        public static string GetFullName(Type baseType, params Type[] additionalInterfaces)
            => GetFullName(DefaultRootNamespace, DefaultSuffix, baseType, additionalInterfaces);

        /// <summary>
        /// Gets the runtime stunt full name from its base type and implemented interfaces.
        /// </summary>
        public static string GetFullName(string @namespace, Type baseType, params Type[] additionalInterfaces)
            => GetFullName(@namespace, DefaultSuffix, baseType, additionalInterfaces);

        /// <summary>
        /// Gets the runtime stunt full name from its base type and implemented interfaces.
        /// </summary>
        public static string GetFullName(string rootNamespace, string suffix, Type baseType, params Type[] additionalInterfaces)
            => GetNamespace(rootNamespace, baseType.Namespace) + "." + GetName(suffix, baseType, additionalInterfaces);

        static string GetNamespace(string rootNamespace, string typeNamespace)
            => string.IsNullOrEmpty(typeNamespace) ? rootNamespace : rootNamespace + "." + typeNamespace;
    }

    static class StringBuilderExtensions
    {
        public static StringBuilder AddNames(this StringBuilder builder, IEnumerable<Type> types)
        {
            foreach (var type in types)
                builder.AddName(type);

            return builder;
        }

        public static StringBuilder AddName(this StringBuilder builder, Type type)
        {
            if (type.IsGenericType)
            {
                builder.Append(type.Name.Substring(0, type.Name.IndexOf('`')));
                if (type.IsConstructedGenericType)
                {
                    return builder.Append("Of").AddNames(type.GenericTypeArguments);
                }
                else
                {
                    return builder.Append("Of").AddNames(type.GetGenericArguments());
                }
            }
            else
            {
                return builder.Append(type.Name);
            }
        }
    }
}
