using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TypeNameFormatter;

namespace Stunts
{
    /// <summary>
    /// Base class for arguments to an invocation, a tuple of the 
    /// declaring <see cref="ParameterInfo"/> and its value.
    /// </summary>
    /// <remarks>
    /// When using <see cref="RawValue"/> and <see cref="WithRawValue"/> 
    /// (to get a new changed record instance), the value is boxed correspondingly 
    /// to the .NET rules for structs. Therefore, it's always recommended to attempt 
    /// casting the base <see cref="Argument"/> to a <see cref="Argument{T}"/> if 
    /// you know the type ahead of time, to avoid unnecessary boxing.
    /// </remarks>
    public abstract record Argument
    {
        /// <summary>
        /// Creates a typed argument.
        /// </summary>
        /// <typeparam name="T">The type of the argument, typically inferred from the <paramref name="value"/> type.</typeparam>
        /// <param name="info">The <see cref="ParameterInfo"/> that declares the argument in a method or constructor.</param>
        /// <param name="value">The argument value.</param>
        public static Argument<T> Create<T>(ParameterInfo info, T value) => new Argument<T>(info, value);

        /// <summary>
        /// Creates the argument with the given <paramref name="parameter"/>.
        /// <param name="parameter">The <see cref="ParameterInfo"/> that declares the argument in a method or constructor.</param>
        /// </summary>
        protected Argument(ParameterInfo parameter) => Parameter = parameter;

        /// <summary>
        /// Gets the name of the argument from <see cref="ParameterInfo.Name"/> 
        /// through the <see cref="Parameter"/> property.
        /// </summary>
        public string Name => Parameter.Name;

        /// <summary>
        /// The <see cref="ParameterInfo"/> that declares the argument in a method or constructor.
        /// </summary>
        public ParameterInfo Parameter { get; init; }

        /// <summary>
        /// Gets the raw, potentially boxed (for value types) value for the argument.
        /// </summary>
        public abstract object? RawValue { get; }

        /// <summary>
        /// Attemps to replace the argument value.
        /// </summary>
        /// <param name="rawValue">The new raw value to attempt setting.</param>
        /// <exception cref="ArgumentException">The new value is not compatible with the argument type.</exception>
        public abstract Argument WithRawValue(object? rawValue);

        /// <summary>
        /// Checks whether the given <paramref name="value"/> is compatible with 
        /// the declared <see cref="Parameter"/> type and nullability constraints.
        /// </summary>
        protected object? CheckValue(object? value)
        {
            var type = Parameter.ParameterType;
            if (Parameter.ParameterType.IsByRef && Parameter.ParameterType.HasElementType)
                type = Parameter.ParameterType.GetElementType();

            if (value != null)
            {
                if (type.IsAssignableFrom(value.GetType()))
                    return value;
                else
                    throw new ArgumentException(ThisAssembly.Strings.ValueNotCompatible(Parameter.Name, value.GetType().GetFormattedName(), type.GetFormattedName()));
            }

            // non-value type and Nullable<T> can handle a null return.
            if (!type.IsValueType ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                return value;

            throw new ArgumentNullException(nameof(value), ThisAssembly.Strings.ValueTypeIsNull(Parameter.Name, type.GetFormattedName()));
        }
    }

    /// <summary>
    /// A generic object-based argument, useful for cases where the type of the value 
    /// isn't known at compile-time.
    /// </summary>
    public record ObjectArgument : Argument
    {
        readonly object? value;

        /// <summary>
        /// Creates the argument with the given <paramref name="parameter"/> and <paramref name="value"/>.
        /// </summary>
        public ObjectArgument(ParameterInfo parameter, object? value)
            : base(parameter) => this.value = CheckValue(value);

        /// <inheritdoc />
        public override object? RawValue => value;

        /// <inheritdoc />
        public override Argument WithRawValue(object? rawValue) => new ObjectArgument(Parameter, CheckValue(rawValue));
    }

    /// <summary>
    /// A strong-typed argument, used when the argument type is known at compile-time, 
    /// which avoids unnecessary value type boxing. Typically created from <see cref="Argument.Create"/> 
    /// factory method.
    /// </summary>
    public record Argument<T> : Argument
    {
        readonly T? value;

        /// <summary>
        /// Creates the argument with the given <paramref name="parameter"/> and <paramref name="value"/>.
        /// </summary>
        public Argument(ParameterInfo parameter, T? value)
            : base(parameter)
            => Value = value;

        /// <summary>
        /// Gets the typed value of the argument.
        /// </summary>
        public T? Value
        {
            get => value;
            init
            {
                var type = Parameter.ParameterType;
                if (Parameter.ParameterType.IsByRef && Parameter.ParameterType.HasElementType)
                    type = Parameter.ParameterType.GetElementType();

                if (!type.IsAssignableFrom(typeof(T)))
                    throw new ArgumentException(ThisAssembly.Strings.TypeNotCompatible(
                        typeof(T).GetFormattedName(), type.GetFormattedName(), Parameter.Name));

                this.value = value;
            }
        }

        /// <inheritdoc />
        public override object? RawValue => Value;

        /// <summary>
        /// Gets a new <see cref="Argument{T}"/> with the given <paramref name="value"/>.
        /// </summary>
        public Argument<T> WithValue(T? value) => new Argument<T>(Parameter, value);

        /// <inheritdoc />
        public override Argument WithRawValue(object? rawValue)
            => rawValue is T value ? new Argument<T>(Parameter, value) : new Argument<T>(Parameter, (T?)CheckValue(rawValue));

        /// <summary>
        /// Implicitly converts a typed argument to the type of its value.
        /// </summary>
        /// <example>
        /// <code>
        /// var arg = Argument.Create(parameter, 42);
        /// // implicit conversion allows direct assignment.
        /// int value = arg;
        /// </code>
        /// </example>
        public static implicit operator T?(Argument<T> argument) => argument.Value;

        /// <inheritdoc />
        [ExcludeFromCodeCoverage]
        [DebuggerNonUserCode]
        public override string ToString() =>
            // Render as: [ref|out]? [type] [name]: [value|null]
            (Parameter.IsOut ? Parameter.ParameterType.GetFormattedName().Replace("ref ", "out ") : Parameter.ParameterType.GetFormattedName()) +
            " " + Parameter.Name +
            (": " + Value == null ? "null" :
                (IsString(Parameter.ParameterType) ? "\"" + Value + "\"" :
                    // render boolean as lowercase to match C#
                    (Value is bool b) ? b.ToString().ToLowerInvariant() :
                    Value)
            );

        static bool IsString(Type type) => type == typeof(string) ||
            (type.IsByRef && type.HasElementType && type.GetElementType() == typeof(string));
    }
}
