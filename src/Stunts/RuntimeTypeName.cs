using System;
using System.Runtime.CompilerServices;
using TypeNameFormatter;

namespace Stunts
{
    static class RuntimeTypeName
    {
        public static string GetDisplayName(this Type type)
        {
#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported)
                return CSharpTypeName.Format(type);
#endif
            return type.GetFormattedName();
        }
    }
}
