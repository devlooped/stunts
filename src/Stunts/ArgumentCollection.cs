using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using TypeNameFormatter;

namespace Stunts
{
    /// <summary>
    /// Contains the arguments of a method invocation as well as their 
    /// parameter information.
    /// </summary>
    //[DebuggerTypeProxy(typeof(DebugView))]
    //[DebuggerDisplay("Count = {Count}")]
    public partial class ArgumentCollection : IArgumentCollection
    {
        readonly Dictionary<string, Argument> arguments;

        /// <summary>
        /// Creates a new argument collection.
        /// </summary>
        public ArgumentCollection(params Argument[] arguments)
            : this(arguments.ToDictionary(x => x.Parameter.Name), arguments.Select(x => x.Parameter).ToArray())
        { }

        /// <summary>
        /// Creates a new argument collection with the given parameters, where the 
        /// argument values are provided using object initializer syntax.
        /// </summary>
        /// <example>
        /// This constructor overload, in combination with the <see cref="Add{T}(string, T)"/> and 
        /// <see cref="Add{T}(int, T)"/> methods, enables the following usage:
        /// <c>
        /// var args = new ArgumentCollection(method.GetParameters())
        /// {
        ///   { "foo", "bar" },
        ///   { "value", 23 },
        ///   { "enabled", true }
        /// };
        /// </c>
        /// This is an alternative to the <c>ArgumentCollection.Create</c> factory methods that 
        /// might be more intuitive or appropriate depending on the needs (i.e. compile-time 
        /// code generation uses this syntax).
        /// </example>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ArgumentCollection(ParameterInfo[] parameters)
            : this(new Dictionary<string, Argument>(), parameters)
        { }

        ArgumentCollection(Dictionary<string, Argument> arguments, ParameterInfo[] parameters)
            => (this.arguments, Parameters)
            = (arguments, parameters);

        /// <summary>
        /// The <see cref="ParameterInfo"/> definitions for all arguments in the collection.
        /// </summary>
        public ParameterInfo[] Parameters { get; }

        /// <inheritdoc />
        public int Count => Parameters.Length;

        /// <inheritdoc />
        public bool Contains(string name) => arguments.ContainsKey(name);

        /// <inheritdoc />
        public Argument this[int index] => index >= 0 && index < Parameters.Length
                ? arguments.TryGetValue(Parameters[index].Name, out var argument)
                ? argument
                : throw new KeyNotFoundException(ThisAssembly.Strings.ArgumentNotFound(Parameters[index].Name))
                : throw new IndexOutOfRangeException(ThisAssembly.Strings.ArgumentIndexNotFound(index));

        /// <inheritdoc />
        public Argument this[string name]
        {
            get => arguments.TryGetValue(name, out var argument)
                ? argument
                : throw new KeyNotFoundException(ThisAssembly.Strings.ArgumentNotFound(name));
            set => arguments[name] = value;
        }

        /// <summary>
        /// Gets the value of the argument with the given name.
        /// </summary>
        /// <param name="name">Name of the argument to retrieve.</param>
        /// <returns>The <see cref="Argument.RawValue"/>.</returns>
        /// <exception cref="KeyNotFoundException">The collection does not contain an argument value with the given name.</exception>
        public object? GetValue(string name) => arguments.TryGetValue(name, out var argument)
            ? argument.RawValue
            : throw new KeyNotFoundException(ThisAssembly.Strings.ArgumentNotFound(name));

        /// <summary>
        /// Gets the value of the argument at the given index.
        /// </summary>
        /// <param name="index">Index of argument to retrieve.</param>
        /// <returns>The <see cref="Argument.RawValue"/>.</returns>
        /// <exception cref="IndexOutOfRangeException">The index is outside of the bounds of <see cref="Parameters"/>.</exception>
        public object? GetValue(int index) => GetValue(Parameters[index].Name);

        /// <summary>
        /// Sets the raw value of the argument with the given name.
        /// </summary>
        /// <param name="name">Name of the argument to assign.</param>
        /// <param name="value">The value of the argument, which must be compatible with the 
        /// corresponding <see cref="ParameterInfo"/> from <see cref="Parameters"/>.</param>
        /// <returns>The <see cref="Argument.RawValue"/>.</returns>
        /// <exception cref="KeyNotFoundException">The collection does not contain an argument value with the given name.</exception>
        public IArgumentCollection SetValue(string name, object? value)
        {
            if (!arguments.TryGetValue(name, out var argument))
                throw new KeyNotFoundException(ThisAssembly.Strings.ArgumentNotFound(name));

            arguments[name] = argument.WithRawValue(value);

            return this;
        }

        /// <summary>
        /// Sets the value of the argument at the given index.
        /// </summary>
        /// <param name="index">Index of argument to assign.</param>
        /// <param name="value">The value of the argument, which must be compatible with the 
        /// corresponding <see cref="ParameterInfo"/> from <see cref="Parameters"/>.</param>
        /// <returns>The <see cref="Argument.RawValue"/>.</returns>
        /// <exception cref="IndexOutOfRangeException">The index is outside of the bounds of <see cref="Parameters"/>.</exception>
        public IArgumentCollection SetValue(int index, object? value)
        {
            if (index < 0 || index >= Parameters.Length)
                throw new IndexOutOfRangeException(ThisAssembly.Strings.ArgumentIndexNotFound(index));

            return SetValue(Parameters[index].Name, value);
        }

        /// <summary>
        /// Sets (or adds) the value of the parameter with the given name.
        /// </summary>
        /// <typeparam name="T">Type of the value being set.</typeparam>
        /// <param name="name">Name of the argument being set.</param>
        /// <param name="value">Value of the argument.</param>
        /// <remarks>
        /// This method is similar to <see cref="SetValue(int, object?)"/> but 
        /// can also add values to parameters which haven't been assigned a value 
        /// yet, and supports object initialization syntax so you can write code like:
        /// <code>
        /// var args = new ArgumentCollection(parameters) 
        /// {
        ///   { "message", "hello" },
        ///   { "count", 25 },
        ///   { "enabled", true },
        /// };
        /// </code>
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Add<T>(string name, T? value)
        {
            if (!arguments.TryGetValue(name, out var argument))
            {
                var parameter = Parameters.FirstOrDefault(p => p.Name == name);
                if (parameter == null)
                    throw new KeyNotFoundException(ThisAssembly.Strings.ArgumentNotFound(name));

                argument = Argument.Create(parameter, value);
                arguments[name] = argument;
            }
            else
            {
                // Avoid boxing whenever possible.
                if (argument is Argument<T> typed)
                    arguments[name] = typed.WithValue(value);
                else
                    arguments[name] = argument.WithRawValue(value);
            }
        }

        /// <summary>
        /// Sets the value of the parameter with the given index.
        /// </summary>
        /// <typeparam name="T">Type of the value being set.</typeparam>
        /// <param name="index">Index of the argument being set.</param>
        /// <param name="value">Value of the argument.</param>
        /// <remarks>
        /// This method is equivalent to <see cref="SetValue(int, object?)"/> and 
        /// supports object initialization syntax so you can write code like:
        /// <code>
        /// var args = new ArgumentCollection(parameters) 
        /// {
        ///   { 0, "hello" },
        ///   { 1, 25 },
        ///   { 2, true },
        /// };
        /// </code>
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Add<T>(int index, T? value) => Add(Parameters[index].Name, value);

        /// <inheritdoc />
        public IEnumerator<Argument> GetEnumerator() => arguments.Values.GetEnumerator();

        /// <inheritdoc />
        [DebuggerNonUserCode]
        [ExcludeFromCodeCoverage]
        public override string ToString() => string.Join(", ", Parameters.Select(ToString));

        [ExcludeFromCodeCoverage]
        [DebuggerNonUserCode]
        string ToString(ParameterInfo parameter) =>
            (parameter.IsOut ? parameter.ParameterType.GetFormattedName().Replace("ref ", "out ") : parameter.ParameterType.GetFormattedName()) +
            " " + parameter.Name +
            (parameter.IsOut ? "" :
                (": " + (!arguments.TryGetValue(parameter.Name, out var argument) ? "null" :
                    ((IsString(parameter.ParameterType) && argument.RawValue != null) ? "\"" + argument.RawValue + "\"" :
                        // render boolean as lowercase to match C#
                        (argument.RawValue is bool b) ? b.ToString().ToLowerInvariant() :
                        argument.RawValue ?? "null"))
                )
            );

        static bool IsString(Type type) => type == typeof(string) ||
            (type.IsByRef && type.HasElementType && type.GetElementType() == typeof(string));

        /// <inheritdoc />
        public override bool Equals(object obj)
            => obj is IArgumentCollection collection && this.SequenceEqual(collection);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var arg in arguments.Values)
                hash.Add(arg);

            return hash.ToHashCode();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [ExcludeFromCodeCoverage]
        [DebuggerNonUserCode]
        class DebugView
        {
            readonly ArgumentCollection arguments;
            public DebugView(ArgumentCollection arguments) => this.arguments = arguments;

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<ParameterInfo, object?>[] Items => arguments.Parameters
                // TODO: get value display, not the actual value??
                .Select(info => new KeyValuePair<ParameterInfo, object?>(info, arguments.GetValue(info.Name)))
                .ToArray();
        }
    }
}
