using System;
using System.Reflection;
using System.Text;

namespace Stunts
{
    /// <summary>
    /// Stable member signatures shared by the data-annotation source generator and
    /// <see cref="DataAnnotationsRegistry"/> lookups.
    /// </summary>
    /// <remarks>
    /// A signature is the comma-separated parameter types. Nested types use a dot.
    /// Arrays keep their rank, by-ref parameters end in <c>&amp;</c>, pointers end in <c>*</c>,
    /// and constructed generics list their arguments. A type parameter of the containing type is
    /// <c>!n</c>. A type parameter of a generic method is <c>!!n</c>.
    /// Generic methods are formatted from their generic method definition, so a closed call
    /// <c>M&lt;int&gt;(int)</c> still matches the definition's <c>!!0</c>.
    /// </remarks>
    public static class DataAnnotationsSignature
    {
        /// <summary>Formats <paramref name="type"/> the way registrations and lookups compare it.</summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="byRef"><see langword="true"/> to append <c>&amp;</c>.</param>
        public static string Format(Type type, bool byRef = false)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var builder = new StringBuilder();
            Append(builder, type);
            if (byRef)
                builder.Append('&');
            return builder.ToString();
        }

        /// <summary>Formats <c>!!ordinal</c>, a generic method type parameter.</summary>
        public static string MethodTypeParameter(int ordinal) => "!!" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Joins parameter signatures with commas.</summary>
        public static string Join(params string[] parts) => parts == null || parts.Length == 0 ? "" : string.Join(",", parts);

        /// <summary>Formats the parameters of <paramref name="method"/>, using its generic method definition when the method is generic.</summary>
        /// <param name="method">The member whose parameters are formatted.</param>
        public static string Format(MethodBase method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            var parameters = method.GetParameters();
            if (method.IsGenericMethod)
                parameters = ((MethodInfo)method).GetGenericMethodDefinition().GetParameters();

            if (parameters.Length == 0)
                return "";

            var builder = new StringBuilder();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i != 0)
                    builder.Append(',');
                Append(builder, parameters[i].ParameterType);
            }

            return builder.ToString();
        }

        static void Append(StringBuilder builder, Type type)
        {
            if (type.IsByRef)
            {
                Append(builder, type.GetElementType()!);
                builder.Append('&');
                return;
            }

            if (type.IsPointer)
            {
                Append(builder, type.GetElementType()!);
                builder.Append('*');
                return;
            }

            if (type.IsArray)
            {
                Append(builder, type.GetElementType()!);
                builder.Append('[');
                if (type.GetArrayRank() > 1)
                    builder.Append(',', type.GetArrayRank() - 1);
                builder.Append(']');
                return;
            }

            if (type.IsGenericParameter)
            {
                builder.Append(type.DeclaringMethod != null ? "!!" : "!");
                builder.Append(type.GenericParameterPosition.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            var chain = type;
            var depth = 0;
            for (var current = type; current != null; current = current.DeclaringType)
                depth++;

            var types = new Type[depth];
            for (var current = type; current != null; current = current.DeclaringType)
                types[--depth] = current;

            var arguments = chain.IsGenericType ? chain.GetGenericArguments() : Type.EmptyTypes;
            var consumed = 0;
            for (var i = 0; i < types.Length; i++)
            {
                if (i != 0)
                    builder.Append('.');

                var current = types[i];
                if (i == 0 && !string.IsNullOrEmpty(current.Namespace))
                {
                    builder.Append(current.Namespace);
                    builder.Append('.');
                }

                var name = current.Name;
                var arity = 0;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                {
                    arity = int.Parse(name.Substring(tick + 1), System.Globalization.CultureInfo.InvariantCulture);
                    name = name.Substring(0, tick);
                }

                builder.Append(name);
                if (arity == 0)
                    continue;

                builder.Append('<');
                for (var argument = 0; argument < arity; argument++)
                {
                    if (argument != 0)
                        builder.Append(',');
                    Append(builder, arguments[consumed++]);
                }

                builder.Append('>');
            }
        }
    }
}
