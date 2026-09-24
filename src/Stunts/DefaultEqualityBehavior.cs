using System.Runtime.CompilerServices;

namespace Stunts
{
    /// <summary>
    /// A <see cref="IStuntBehavior"/> that implements a stunt's 
    /// <c>GetHashCode</c> and <c>Equals</c> using <see cref="object.GetHashCode"/> 
    /// and <see cref="object.ReferenceEquals(object, object)"/> respectively.
    /// </summary>
    public class DefaultEqualityBehavior : IStuntBehavior
    {
        /// <summary>
        /// Always returns <see langword="true" />
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation)
            => invocation.MethodBase.Name == nameof(GetHashCode) || invocation.MethodBase.Name == nameof(Equals);

        /// <summary>
        /// Implements the <see cref="object.GetHashCode"/> and <see cref="object.Equals(object)"/> 
        /// methods.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            if (invocation.MethodBase.Name == nameof(GetHashCode))
                return invocation.CreateValueReturn(RuntimeHelpers.GetHashCode(invocation.Target), invocation.Arguments);
            if (invocation.MethodBase.Name == nameof(Equals))
                return invocation.CreateValueReturn(ReferenceEquals(invocation.Target, invocation.Arguments.GetValue(0)), invocation.Arguments);

            return next.Invoke(invocation, next);
        }
    }
}