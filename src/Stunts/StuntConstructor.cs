using System;
using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// A generated, statically typed constructor of a compile-time stunt.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StuntConstructor
    {
        /// <summary>Initializes the constructor.</summary>
        /// <param name="parameterTypes">The constructor parameter types.</param>
        /// <param name="create">Creates the stunt from arguments matching <paramref name="parameterTypes"/>.</param>
        public StuntConstructor(Type[] parameterTypes, Func<object?[], object> create)
            => (ParameterTypes, Create) = (parameterTypes ?? throw new ArgumentNullException(nameof(parameterTypes)),
                create ?? throw new ArgumentNullException(nameof(create)));

        /// <summary>Gets the constructor parameter types.</summary>
        public Type[] ParameterTypes { get; }

        /// <summary>Gets the function that creates the stunt.</summary>
        public Func<object?[], object> Create { get; }

        internal bool Accepts(object?[] arguments)
        {
            if (arguments.Length != ParameterTypes.Length)
                return false;

            for (var i = 0; i < arguments.Length; i++)
            {
                var type = ParameterTypes[i];
                if (arguments[i] is object argument
                    ? !type.IsInstanceOfType(argument)
                    : type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                    return false;
            }

            return true;
        }

        // A null argument is not more specific than any type, so it doesn't decide.
        internal bool IsMoreSpecificThan(StuntConstructor other, object?[] arguments)
        {
            var better = false;
            for (var i = 0; i < ParameterTypes.Length; i++)
            {
                var mine = ParameterTypes[i];
                var theirs = other.ParameterTypes[i];
                if (mine == theirs)
                    continue;
                if (theirs.IsAssignableFrom(mine))
                    better = true;
                else if (mine.IsAssignableFrom(theirs) || arguments[i] != null)
                    return false;
            }

            return better;
        }
    }
}
