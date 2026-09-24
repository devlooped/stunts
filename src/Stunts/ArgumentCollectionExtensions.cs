using System;
using System.ComponentModel;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// Usability overloads for <see cref="IArgumentCollection"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ArgumentCollectionExtensions
    {
        /// <summary>
        /// Sets a typed argument on the <paramref name="arguments"/> collection 
        /// with the given <paramref name="name"/>.
        /// </summary>
        public static IArgumentCollection Set<T>(this IArgumentCollection arguments, string name, T value)
        {
            var argument = arguments[name];
            if (argument is Argument<T> typed)
                arguments[name] = typed.WithValue(value);
            else
                arguments[name] = argument.WithRawValue(value);

            return arguments;
        }

        /// <summary>
        /// Sets a typed argument on the <paramref name="arguments"/> collection 
        /// with the given <paramref name="index"/>.
        /// </summary>
        public static IArgumentCollection Set<T>(this IArgumentCollection arguments, int index, T value)
        {
            var argument = arguments[index];
            if (argument is Argument<T> typed)
                arguments[argument.Parameter.Name] = typed.WithValue(value);
            else
                arguments[argument.Parameter.Name] = argument.WithRawValue(value);

            return arguments;
        }

        /// <summary>
        /// Gets a typed argument from the <paramref name="arguments"/> collection 
        /// with the given <paramref name="name"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value in the collection was <see langword="null"/>.</exception>
        public static T Get<T>(this IArgumentCollection arguments, string name) where T : notnull
            => Get<T>(arguments[name]);

        /// <summary>
        /// Gets a typed argument from the <paramref name="arguments"/> collection 
        /// with the given <paramref name="index"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value in the collection was <see langword="null"/>.</exception>
        public static T Get<T>(this IArgumentCollection arguments, int index) where T : notnull
            => Get<T>(arguments[index]);

        /// <summary>
        /// Gets an optional (nullable) typed argument from the <paramref name="arguments"/> collection 
        /// with the given <paramref name="name"/>.
        /// </summary>
        public static T? GetNullable<T>(this IArgumentCollection arguments, string name)
            => GetNullable<T>(arguments[name]);

        /// <summary>
        /// Gets an optional (nullable) typed argument from the <paramref name="arguments"/> collection 
        /// with the given <paramref name="index"/>.
        /// </summary>
        public static T? GetNullable<T>(this IArgumentCollection arguments, int index)
            => GetNullable<T>(arguments[index]);

        static T Get<T>(Argument argument) where T : notnull
        {
            if (argument is Argument<T> typed)
                return typed.Value!;

            var type = typeof(T);
            var value = argument.RawValue;
            if (value == null)
            {
                // Nullable<T> can handle a null return, and in that case 
                // the caller explicitly opted-in to nulls. This could only 
                // happen if the caller does not have nullable turned on.
                if (type.IsValueType &&
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return default!;
                else if (type.IsValueType)
                    throw new ArgumentNullException(argument.Parameter.Name, ThisAssembly.Strings.ValueTypeIsNull(argument.Parameter.Name, type));

                throw new ArgumentNullException(argument.Parameter.Name, ThisAssembly.Strings.ValueIsNull(argument.Parameter.Name));
            }

            if (type.IsAssignableFrom(value.GetType()))
                return (T)value;

            throw new ArgumentException(ThisAssembly.Strings.ValueNotCompatible(argument, value.GetType(), type));
        }

        static T? GetNullable<T>(Argument argument)
        {
            if (argument is Argument<T> typed)
                return typed.Value;

            var type = typeof(T);
            var value = argument.RawValue;
            if (value == null)
            {
                // non-value type or Nullable<T> can handle a null return.
                if (!type.IsValueType ||
                    type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return default;

                throw new ArgumentNullException(argument.Parameter.Name, ThisAssembly.Strings.ValueIsNull(argument.Parameter.Name));
            }

            if (type.IsAssignableFrom(value.GetType()))
                return (T)value;

            throw new ArgumentException(ThisAssembly.Strings.ValueNotCompatible(argument, value.GetType(), type));
        }
    }
}
