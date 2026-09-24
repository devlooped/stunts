using System;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// Contains a factory methods for <see cref="Ref{T}"/>.
    /// </summary>
    public class Ref
    {
        /// <summary>
        /// Creates an instance of the <see cref="Ref{T}"/> class with the given <paramref name="value"/>.
        /// </summary>
        public static Ref<T> Create<T>(T value) => new Ref<T>(value);

        /// <summary>
        /// Creates an instance of the <see cref="Ref{T}"/> class for the 
        /// given <paramref name="type"/> using the given <paramref name="value"/>.
        /// </summary>
        /// <exception cref="ArgumentException">The <paramref name="type"/> is <see langword="void"/> or the 
        /// <paramref name="value"/> cannot be casted to <paramref name="type"/>.</exception>
        public static object Create(Type type, object? value)
        {
            if (type == typeof(void))
                throw new ArgumentException(ThisAssembly.Strings.RefVoid, nameof(type));

            try
            {
                var ctor = typeof(Ref<>).MakeGenericType(type).GetConstructors()[0];
                // By invoking the constructor directly we get proper InvalidCastException
                return ctor.Invoke(new object?[] { value });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }
    }

    /// <summary>
    /// A utility class to hold a value that can be used for 
    /// ref returns APIs.
    /// </summary>
    /// <remarks>
    /// An instance of this class can be set as the <see cref="IMethodReturn.ReturnValue"/> 
    /// (i.e. when calling <see cref="IMethodInvocation.CreateValueReturn"/>)
    /// for cases where the method signature uses ref returns.
    /// See https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/ref-returns.
    /// </remarks>
    /// <typeparam name="T">Type of reference to hold.</typeparam>
    public class Ref<T> : Ref
    {
        T? value;

        /// <summary>
        /// Creates an instance of the wrapping class.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        public Ref(T? value) => this.value = value;

        /// <summary>
        /// Gets the wrapped value by reference.
        /// </summary>
        public ref T? Value => ref value;

        /// <summary>
        /// Implicitly converts a value to a wrapped <see cref="Ref{T}"/> with it 
        /// as its <see cref="Ref{T}.Value"/>.
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Ref<T>(T? value) => new Ref<T>(value);

        /// <summary>
        /// Implicitly converts a wrapped <see cref="Ref{T}"/> to <typeparamref name="T"/> 
        /// by retrieving its its <see cref="Ref{T}.Value"/>.
        /// </summary>
        /// <param name="ref"></param>
        public static implicit operator T?(Ref<T> @ref) => @ref.Value;
    }
}
