using System;
using System.Reflection;
using System.Threading;

namespace Stunts
{
    /// <summary>
    /// Forwards an invocation to another instance, or to an instance created on first use.
    /// </summary>
    /// <remarks>
    /// Constructors and members named <c>Equals</c>, <c>GetHashCode</c>, or <c>ToString</c>
    /// are left to the rest of the pipeline. A factory that returns <see langword="null"/>
    /// does the same, so a class stunt runs its base implementation and an interface stunt
    /// reports <see cref="NotImplementedException"/>. The factory runs once for this behavior.
    /// </remarks>
    public class TargetBehavior : IStuntBehavior
    {
        readonly object? instance;
        readonly Lazy<object?>? factory;

        /// <summary>
        /// Forwards to <paramref name="target"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
        public TargetBehavior(object target)
            => instance = target ?? throw new ArgumentNullException(nameof(target));

        /// <summary>
        /// Forwards to the instance <paramref name="factory"/> returns on the first forwarded call.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
        public TargetBehavior(Func<object?> factory)
            => this.factory = new Lazy<object?>(
                factory ?? throw new ArgumentNullException(nameof(factory)),
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Skips constructors and the object identity members.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation)
            => invocation.MethodBase is not ConstructorInfo
                && invocation.MethodBase.Name is not (nameof(Equals) or nameof(GetHashCode) or nameof(ToString));

        /// <summary>
        /// Invokes the same member on the target. The target's exception propagates unchanged.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var target = instance ?? factory!.Value;
            return target == null ? next.Invoke(invocation, next) : invocation.Invoke(target);
        }
    }
}
