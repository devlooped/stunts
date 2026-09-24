using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// Usability overloads for working with <see cref="IMethodReturn"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MethodReturnExtensions
    {
        /// <summary>
        /// Returns the <see cref="IMethodReturn.ReturnValue"/> as a 
        /// <see cref="Ref{T}"/>.
        /// </summary>
        /// <remarks>
        /// If the <see cref="IMethodReturn.ReturnValue"/> is already an 
        /// instance of <see cref="Ref{T}"/>, it's returned as-is. Otherwise, 
        /// the value is wrapped in a new <see cref="Ref{T}"/>.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static Ref<T> AsRef<T>(this IMethodReturn @return)
        {
            if (@return.ReturnValue is Ref<T> wrapped)
                return wrapped;

            return new Ref<T>((T)@return.ReturnValue!);
        }
    }
}
