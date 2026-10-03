using System;
using System.Collections.Generic;
using System.Linq;

namespace Stunts
{
    /// <summary>
    /// Formats types using fully qualified C# syntax, so they can be pasted in source, 
    /// without reflecting over members (which may not be available under Native AOT).
    /// </summary>
    static class CSharpTypeName
    {
        static readonly Dictionary<Type, string> keywords = new()
        {
            [typeof(bool)] = "bool",
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(char)] = "char",
            [typeof(decimal)] = "decimal",
            [typeof(double)] = "double",
            [typeof(float)] = "float",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(object)] = "object",
            [typeof(string)] = "string",
            [typeof(void)] = "void",
        };

        public static string Format(Type type)
        {
            if (keywords.TryGetValue(type, out var keyword))
                return keyword;
            if (type.IsArray)
                return Format(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsPointer)
                return Format(type.GetElementType()!) + "*";
            if (type.IsByRef)
                return Format(type.GetElementType()!);
            if (type.IsGenericParameter)
                return type.Name;
            if (Nullable.GetUnderlyingType(type) is Type underlying)
                return Format(underlying) + "?";

            var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            return Format(type, arguments, arguments.Length);
        }

        // Distributes the type arguments of a nested type across its declaring types.
        static string Format(Type type, Type[] arguments, int count)
        {
            var arity = 0;
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                arity = int.Parse(name.Substring(tick + 1));
                name = name.Substring(0, tick);
            }

            if (arity > 0)
                name += "<" + string.Join(", ", arguments.Skip(count - arity).Take(arity).Select(Format)) + ">";

            if (type.DeclaringType is Type declaring)
                return Format(declaring, arguments, count - arity) + "." + name;

            return string.IsNullOrEmpty(type.Namespace) ? name : type.Namespace + "." + name;
        }
    }
}
