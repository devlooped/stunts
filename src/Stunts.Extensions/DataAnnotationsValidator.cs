using System;
using System.Buffers.Text;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stunts
{
    /// <summary>
    /// AOT-safe checks for the built-in validation attributes in
    /// <see cref="System.ComponentModel.DataAnnotations"/>.
    /// </summary>
    /// <remarks>
    /// Each method returns <see langword="null"/> when the value is acceptable and a
    /// <see cref="ValidationResult"/> otherwise. Null values pass every check except
    /// <see cref="Required"/>, <see cref="Allowed"/>, and <see cref="Denied"/>, matching
    /// the built-in attributes. The source generator emits calls with the attribute
    /// arguments inlined. Nothing in this type reflects over attributes or members.
    /// </remarks>
    public static class DataAnnotationsValidator
    {
        /// <summary>Checks <see cref="RequiredAttribute"/>.</summary>
        public static ValidationResult? Required(object? value, string memberName, string? displayName, bool allowEmptyStrings, string? errorMessage)
        {
            if (value is not null && (allowEmptyStrings || value is not string text || !string.IsNullOrWhiteSpace(text)))
                return null;

            return Result(errorMessage, "The {0} field is required.", memberName, displayName);
        }

        /// <summary>Checks <see cref="StringLengthAttribute"/>. Non-strings throw <see cref="InvalidCastException"/>.</summary>
        public static ValidationResult? StringLength(object? value, int minimumLength, int maximumLength, string memberName, string? displayName, string? errorMessage)
        {
            if (maximumLength < 0)
                throw new InvalidOperationException("The maximum length must be a nonnegative integer.");
            if (maximumLength < minimumLength)
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The maximum value '{0}' must be greater than or equal to the minimum value '{1}'.", maximumLength, minimumLength));
            if (value == null)
                return null;

            var length = ((string)value).Length;
            if (length >= minimumLength && length <= maximumLength)
                return null;

            var fallback = minimumLength != 0 && errorMessage == null
                ? "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}."
                : "The field {0} must be a string with a maximum length of {1}.";
            return Result(errorMessage, fallback, memberName, displayName, maximumLength, minimumLength);
        }

        /// <summary>Checks <see cref="MinLengthAttribute"/>.</summary>
        public static ValidationResult? MinLength(object? value, int length, string memberName, string? displayName, string? errorMessage, Func<object, int>? count = null)
        {
            if (length < 0)
                throw new InvalidOperationException("MinLengthAttribute must have a Length value that is zero or greater.");
            if (value == null || CountOf(value, count) >= length)
                return null;

            return Result(errorMessage, "The field {0} must be a string or array type with a minimum length of '{1}'.", memberName, displayName, length);
        }

        /// <summary>Checks <see cref="MaxLengthAttribute"/>. A <paramref name="length"/> of -1 allows any count.</summary>
        public static ValidationResult? MaxLength(object? value, int length, string memberName, string? displayName, string? errorMessage, Func<object, int>? count = null)
        {
            if (length == 0 || length < -1)
                throw new InvalidOperationException("MaxLengthAttribute must have a Length value that is greater than zero. Use MaxLength() without parameters to indicate that the string or array can have the maximum allowable length.");
            if (value == null || length == -1 || CountOf(value, count) <= length)
                return null;

            return Result(errorMessage, "The field {0} must be a string or array type with a maximum length of '{1}'.", memberName, displayName, length);
        }

        /// <summary>Checks <see cref="LengthAttribute"/>.</summary>
        public static ValidationResult? Length(object? value, int minimumLength, int maximumLength, string memberName, string? displayName, string? errorMessage, Func<object, int>? count = null)
        {
            if (minimumLength < 0)
                throw new InvalidOperationException("LengthAttribute must have a MinimumLength value that is zero or greater.");
            if (maximumLength < minimumLength)
                throw new InvalidOperationException("LengthAttribute must have a MaximumLength value that is greater than or equal to MinimumLength.");
            if (value == null)
                return null;

            var length = CountOf(value, count);
            if (length >= minimumLength && length <= maximumLength)
                return null;

            return Result(errorMessage, "The field {0} must be a string or collection type with a minimum length of '{1}' and maximum length of '{2}'.", memberName, displayName, minimumLength, maximumLength);
        }

        /// <summary>
        /// Checks <see cref="RangeAttribute"/> for an <see cref="int"/> or <see cref="double"/> bound.
        /// Other bound types use the parser overload. Null and empty strings pass.
        /// </summary>
        public static ValidationResult? Range<T>(object? value, T minimum, T maximum, bool minimumIsExclusive, bool maximumIsExclusive, string memberName, string? displayName, string? errorMessage)
            where T : IComparable<T>
        {
            EnsureOrdered(minimum, maximum, minimumIsExclusive, maximumIsExclusive);
            if (value is null or string { Length: 0 })
                return null;
            if (!TryConvert(value, out T converted))
                return RangeResult(minimum, maximum, minimumIsExclusive, maximumIsExclusive, memberName, displayName, errorMessage);

            return InRange(converted, minimum, maximum, minimumIsExclusive, maximumIsExclusive)
                ? null
                : RangeResult(minimum, maximum, minimumIsExclusive, maximumIsExclusive, memberName, displayName, errorMessage);
        }

        /// <summary>
        /// Checks <see cref="RangeAttribute"/> constructed from strings parsed by <paramref name="parse"/>.
        /// </summary>
        public static ValidationResult? Range<T>(object? value, string minimum, string maximum, DataAnnotationsParser<T> parse, bool parseLimitsInInvariantCulture, bool convertValueInInvariantCulture, bool minimumIsExclusive, bool maximumIsExclusive, string memberName, string? displayName, string? errorMessage)
            where T : IComparable<T>
        {
            if (parse == null)
                throw new ArgumentNullException(nameof(parse));

            var limits = parseLimitsInInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
            if (!parse(minimum, limits, out var min) || !parse(maximum, limits, out var max))
                throw new FormatException("The minimum and maximum values must be set.");

            EnsureOrdered(min, max, minimumIsExclusive, maximumIsExclusive);
            if (value is null or string { Length: 0 })
                return null;

            T converted;
            if (value is T typed)
            {
                converted = typed;
            }
            else if (value is string text)
            {
                var culture = convertValueInInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
                if (!parse(text, culture, out converted))
                    return RangeResult(min, max, minimumIsExclusive, maximumIsExclusive, memberName, displayName, errorMessage);
            }
            else
            {
                return RangeResult(min, max, minimumIsExclusive, maximumIsExclusive, memberName, displayName, errorMessage);
            }

            return InRange(converted, min, max, minimumIsExclusive, maximumIsExclusive)
                ? null
                : RangeResult(min, max, minimumIsExclusive, maximumIsExclusive, memberName, displayName, errorMessage);
        }

        /// <summary>Checks <see cref="RegularExpressionAttribute"/>. Null and empty strings pass. The pattern must match the entire value.</summary>
        public static ValidationResult? Pattern(object? value, Regex pattern, string patternText, string memberName, string? displayName, string? errorMessage)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            var text = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(text))
                return null;

            foreach (var match in pattern.EnumerateMatches(text))
            {
                if (match.Index == 0 && match.Length == text.Length)
                    return null;
                break;
            }

            return Result(errorMessage, "The field {0} must match the regular expression '{1}'.", memberName, displayName, patternText);
        }

        /// <summary>Checks <see cref="CompareAttribute"/> against a value the caller already read.</summary>
        public static ValidationResult? Compare(object? value, object? other, string memberName, string? displayName, string otherDisplayName, string? errorMessage)
        {
            if (Equals(value, other))
                return null;

            return Result(errorMessage, "'{0}' and '{1}' do not match.", memberName, displayName, otherDisplayName);
        }

        /// <summary>Checks <see cref="EmailAddressAttribute"/>.</summary>
        public static ValidationResult? EmailAddress(object? value, string memberName, string? displayName, string? errorMessage)
        {
            if (value == null)
                return null;
            if (value is string text && IsEmail(text))
                return null;

            return Result(errorMessage, "The {0} field is not a valid e-mail address.", memberName, displayName);
        }

        /// <summary>Checks <see cref="PhoneAttribute"/>.</summary>
        public static ValidationResult? Phone(object? value, string memberName, string? displayName, string? errorMessage)
        {
            if (value == null)
                return null;
            if (value is string text && IsPhone(text))
                return null;

            return Result(errorMessage, "The {0} field is not a valid phone number.", memberName, displayName);
        }

        /// <summary>Checks <see cref="CreditCardAttribute"/> with the Luhn test, ignoring spaces and dashes.</summary>
        public static ValidationResult? CreditCard(object? value, string memberName, string? displayName, string? errorMessage)
        {
            if (value == null)
                return null;
            if (value is string text && IsCreditCard(text))
                return null;

            return Result(errorMessage, "The {0} field is not a valid credit card number.", memberName, displayName);
        }

        /// <summary>Checks <see cref="UrlAttribute"/>. Absolute <see cref="Uri"/> values and http, https, and ftp strings pass.</summary>
        public static ValidationResult? Url(object? value, string memberName, string? displayName, string? errorMessage)
        {
            var valid = value switch
            {
                null => true,
                Uri uri when uri.IsAbsoluteUri => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp,
                string text => text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            return valid ? null : Result(errorMessage, "The {0} field is not a valid fully-qualified http, https, or ftp URL.", memberName, displayName);
        }

        /// <summary>Checks <see cref="Base64StringAttribute"/>.</summary>
        public static ValidationResult? Base64String(object? value, string memberName, string? displayName, string? errorMessage)
        {
            if (value == null)
                return null;
            if (value is string text && Base64.IsValid(text.AsSpan()))
                return null;

            return Result(errorMessage, "The {0} field is not a valid Base64 encoding.", memberName, displayName);
        }

        /// <summary>Checks <see cref="EnumDataTypeAttribute"/>. Null and empty strings pass.</summary>
        public static ValidationResult? Enum<TEnum>(object? value, bool flags, string memberName, string? displayName, string? errorMessage)
            where TEnum : struct, Enum
        {
            if (value == null || value is string { Length: 0 })
                return null;
            if (!TryEnum(value, out TEnum converted))
                return Result(errorMessage, "The field {0} is invalid.", memberName, displayName);
            if (flags ? IsNamedFlag(converted) : System.Enum.IsDefined(converted))
                return null;

            return Result(errorMessage, "The field {0} is invalid.", memberName, displayName);
        }

        /// <summary>Checks <see cref="AllowedValuesAttribute"/>. Null passes only when null is one of the values.</summary>
        public static ValidationResult? Allowed(object? value, string memberName, string? displayName, string? errorMessage, params object?[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (var allowed in values)
            {
                if (allowed is null ? value is null : allowed.Equals(value))
                    return null;
            }

            return Result(errorMessage, "The {0} field does not equal any of the values specified in AllowedValuesAttribute.", memberName, displayName);
        }

        /// <summary>Checks <see cref="DeniedValuesAttribute"/>. Null fails only when null is one of the values.</summary>
        public static ValidationResult? Denied(object? value, string memberName, string? displayName, string? errorMessage, params object?[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            foreach (var denied in values)
            {
                if (denied is null ? value is null : denied.Equals(value))
                    return Result(errorMessage, "The {0} field equals one of the values specified in DeniedValuesAttribute.", memberName, displayName);
            }

            return null;
        }

        /// <summary>Checks <see cref="FileExtensionsAttribute"/>. <paramref name="extensions"/> defaults to png, jpg, jpeg, and gif when blank.</summary>
        public static ValidationResult? FileExtensions(object? value, string? extensions, string memberName, string? displayName, string? errorMessage)
        {
            if (value == null)
                return null;
            if (value is not string text)
                return FileExtensionResult(extensions, memberName, displayName, errorMessage);

            var parsed = ParseExtensions(extensions);
            var extension = System.IO.Path.GetExtension(text).ToLowerInvariant();
            for (var i = 0; i < parsed.Length; i++)
            {
                if (parsed[i] == extension)
                    return null;
            }

            return FileExtensionResult(extensions, memberName, displayName, errorMessage);
        }

        /// <summary>
        /// Normalizes a <see cref="CustomValidationAttribute"/> method result.
        /// A null result passes. An empty error message becomes "{0} is not valid."
        /// </summary>
        public static ValidationResult? CustomResult(ValidationResult? result, string memberName, string? displayName, string? errorMessage)
        {
            if (result == ValidationResult.Success)
                return null;

            if (string.IsNullOrEmpty(result!.ErrorMessage))
                return Result(errorMessage, "{0} is not valid.", memberName, displayName);

            var message = result.ErrorMessage!;
            if (message.Contains("{0}"))
                message = string.Format(CultureInfo.CurrentCulture, message, displayName ?? memberName);

            var names = result.MemberNames;
            var any = false;
            if (names != null)
            {
                foreach (var _ in names)
                {
                    any = true;
                    break;
                }
            }

            return any ? new ValidationResult(message, names) : new ValidationResult(message, new[] { memberName });
        }

        /// <summary>Builds the conversion failure reported by <see cref="CustomValidationAttribute"/>.</summary>
        public static ValidationResult ConversionFailed(object? value, string expectedType, string validatorType, string method, string memberName)
        {
            var message = string.Format(
                CultureInfo.CurrentCulture,
                "Could not convert the value of type '{0}' to '{1}' as expected by method {2}.{3}.",
                value != null ? value.GetType().ToString() : "null",
                expectedType,
                validatorType,
                method);
            return new ValidationResult(message, new[] { memberName });
        }

        static ValidationResult Result(string? errorMessage, string fallback, string memberName, string? displayName, params object?[] args)
        {
            var values = new object?[args.Length + 1];
            values[0] = displayName ?? memberName;
            Array.Copy(args, 0, values, 1, args.Length);
            return new ValidationResult(
                string.Format(CultureInfo.CurrentCulture, errorMessage ?? fallback, values),
                new[] { memberName });
        }

        static int CountOf(object value, Func<object, int>? count)
        {
            if (value is string text)
                return text.Length;
            if (count != null)
                return count(value);
            if (value is ICollection collection)
                return collection.Count;

            throw new InvalidCastException(string.Format(
                CultureInfo.CurrentCulture,
                "The field of type {0} must be a string, array or ICollection type.",
                value.GetType()));
        }

        static bool InRange<T>(T value, T minimum, T maximum, bool minimumIsExclusive, bool maximumIsExclusive)
            where T : IComparable<T>
        {
            var lower = minimum.CompareTo(value);
            var upper = maximum.CompareTo(value);
            var aboveMinimum = minimumIsExclusive ? lower < 0 : lower <= 0;
            var belowMaximum = maximumIsExclusive ? upper > 0 : upper >= 0;
            return aboveMinimum && belowMaximum;
        }

        static void EnsureOrdered<T>(T minimum, T maximum, bool minimumIsExclusive, bool maximumIsExclusive)
            where T : IComparable<T>
        {
            var comparison = minimum.CompareTo(maximum);
            if (comparison > 0)
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The maximum value '{0}' must be greater than or equal to the minimum value '{1}'.", maximum, minimum));
            if (comparison == 0 && (minimumIsExclusive || maximumIsExclusive))
                throw new InvalidOperationException("Cannot use exclusive bounds when the maximum value is equal to the minimum value.");
        }

        static bool TryConvert<T>(object value, out T converted)
        {
            if (value is T typed)
            {
                converted = typed;
                return true;
            }

            try
            {
                if (typeof(T) == typeof(int))
                {
                    converted = (T)(object)Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return true;
                }

                if (typeof(T) == typeof(double))
                {
                    converted = (T)(object)Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch (FormatException)
            {
                converted = default!;
                return false;
            }
            catch (InvalidCastException)
            {
                converted = default!;
                return false;
            }
            catch (NotSupportedException)
            {
                converted = default!;
                return false;
            }

            converted = default!;
            return false;
        }

        static ValidationResult RangeResult(object? minimum, object? maximum, bool minimumIsExclusive, bool maximumIsExclusive, string memberName, string? displayName, string? errorMessage)
        {
            var fallback = (minimumIsExclusive, maximumIsExclusive) switch
            {
                (false, false) => "The field {0} must be between {1} and {2}.",
                (true, false) => "The field {0} must be between {1} exclusive and {2}.",
                (false, true) => "The field {0} must be between {1} and {2} exclusive.",
                _ => "The field {0} must be between {1} exclusive and {2} exclusive."
            };
            return Result(errorMessage, fallback, memberName, displayName, minimum, maximum);
        }

        static bool IsEmail(string value)
        {
            if (value.AsSpan().ContainsAny('\r', '\n'))
                return false;

            var index = value.IndexOf('@');
            return index > 0 && index != value.Length - 1 && index == value.LastIndexOf('@');
        }

        static bool IsPhone(string value)
        {
            var text = RemoveExtension(value.Replace("+", "").TrimEnd());
            var digit = false;
            foreach (var character in text)
            {
                if (char.IsDigit(character))
                {
                    digit = true;
                    break;
                }
            }

            if (!digit)
                return false;

            foreach (var character in text)
            {
                if (!char.IsDigit(character) && !char.IsWhiteSpace(character) && "-.()".IndexOf(character) < 0)
                    return false;
            }

            return true;
        }

        static string RemoveExtension(string value)
        {
            if (TryStrip(value, "ext.", out var stripped) || TryStrip(value, "ext", out stripped) || TryStrip(value, "x", out stripped))
                return stripped;
            return value;
        }

        static bool TryStrip(string value, string marker, out string stripped)
        {
            var index = value.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var extension = value.Substring(index + marker.Length).TrimStart();
                if (extension.Length > 0)
                {
                    foreach (var character in extension)
                    {
                        if (!char.IsDigit(character))
                        {
                            stripped = value;
                            return false;
                        }
                    }

                    stripped = value.Substring(0, index);
                    return true;
                }
            }

            stripped = value;
            return false;
        }

        static bool IsCreditCard(string value)
        {
            var checksum = 0;
            var even = false;
            for (var i = value.Length - 1; i >= 0; i--)
            {
                var digit = value[i];
                if (!char.IsAsciiDigit(digit))
                {
                    if (digit is '-' or ' ')
                        continue;
                    return false;
                }

                var digitValue = (digit - '0') * (even ? 2 : 1);
                even = !even;
                while (digitValue > 0)
                {
                    checksum += digitValue % 10;
                    digitValue /= 10;
                }
            }

            return checksum % 10 == 0;
        }

        static bool TryEnum<TEnum>(object value, out TEnum converted)
            where TEnum : struct, Enum
        {
            switch (value)
            {
                case TEnum typed:
                    converted = typed;
                    return true;
                case string text:
                    return System.Enum.TryParse(text, ignoreCase: false, out converted);
                case bool or float or double or decimal or char:
                    converted = default;
                    return false;
            }

            var type = value.GetType();
            if (type.IsEnum || !type.IsValueType)
            {
                converted = default;
                return false;
            }

            try
            {
                converted = (TEnum)System.Enum.ToObject(typeof(TEnum), value);
                return true;
            }
            catch (ArgumentException)
            {
                converted = default;
                return false;
            }
        }

        static bool IsNamedFlag<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            var name = value.ToString();
            var number = Convert.ChangeType(value, System.Enum.GetUnderlyingType(typeof(TEnum)), CultureInfo.InvariantCulture)!.ToString();
            return !string.Equals(number, name, StringComparison.Ordinal);
        }

        static string[] ParseExtensions(string? extensions)
        {
            var source = string.IsNullOrWhiteSpace(extensions) ? "png,jpg,jpeg,gif" : extensions!;
            var normalized = source.Replace(" ", "").Replace(".", "").ToLowerInvariant();
            var parts = normalized.Split(',');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = "." + parts[i];
            return parts;
        }

        static ValidationResult FileExtensionResult(string? extensions, string memberName, string? displayName, string? errorMessage)
        {
            var parsed = ParseExtensions(extensions);
            return Result(errorMessage, "The {0} field only accepts files with the following extensions: {1}", memberName, displayName, string.Join(", ", parsed));
        }
    }

    /// <summary>Parses one bound of a <see cref="RangeAttribute"/> that was declared as text.</summary>
    /// <typeparam name="T">The operand type.</typeparam>
    public delegate bool DataAnnotationsParser<T>(string text, IFormatProvider? provider, out T result);
}
