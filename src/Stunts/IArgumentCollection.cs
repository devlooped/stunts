using System.Collections.Generic;

namespace Stunts
{
    /// <summary>
    /// Represents the arguments of a method invocation.
    /// </summary>
    public interface IArgumentCollection : IReadOnlyList<Argument>
    {
        /// <summary>
        /// Determines whether the collection contains an argument with the given name.
        /// </summary>
        /// <param name="name">The argument name to lookup.</param>
        bool Contains(string name);

        /// <summary>
        /// Gets the (reference or boxed) value for the argument with the 
        /// given name.
        /// </summary>
        object? GetValue(string name);

        /// <summary>
        /// Gets the (reference or boxed) value for the argument with the 
        /// given index.
        /// </summary>
        object? GetValue(int index);

        /// <summary>
        /// Sets the (reference or boxed) value for the argument with the 
        /// given name.
        /// </summary>
        IArgumentCollection SetValue(string name, object? value);

        /// <summary>
        /// Sets the (reference or boxed) value for the argument with the 
        /// given index.
        /// </summary>
        IArgumentCollection SetValue(int index, object? value);

        /// <summary>
        /// Gets or sets the argument with the given name.
        /// </summary>
        Argument this[string name] { get; set; }
    }
}
