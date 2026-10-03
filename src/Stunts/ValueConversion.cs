using System;

namespace Stunts
{
    static class ValueConversion
    {
        public static object? Validate(object? value, string argumentName, Type expectedType)
        {
            if (value == null &&
                expectedType.IsValueType &&
                Nullable.GetUnderlyingType(expectedType) == null)
            {
                throw new ArgumentNullException(
                    argumentName,
                    ThisAssembly.Strings.ValueTypeIsNull(argumentName, expectedType.GetDisplayName()));
            }

            return value;
        }
    }
}
