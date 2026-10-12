using System;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// Rejects a call when a predicate returns an exception.
    /// </summary>
    /// <remarks>
    /// The predicate inspects the invocation and does not call the rest of the pipeline.
    /// A <see langword="null"/> result continues the pipeline. Any other result fails the
    /// call, and the rest of the pipeline does not run.
    /// <para>
    /// A synchronous member throws that exception on the caller thread. A member that
    /// returns <see cref="Task"/> or <see cref="ValueTask"/> returns a faulted awaitable
    /// of the same type, so the caller observes the exception when it awaits. The target
    /// does not run.
    /// </para>
    /// <para>
    /// The constructor is an invocation too. A behavior added before the stunt is materialized
    /// sees that call. A predicate that rejects it prevents the stunt from being constructed.
    /// </para>
    /// <para>
    /// Add this behavior after the behaviors that should observe the rejection, and ahead
    /// of the behaviors that should see only accepted calls. The pipeline runs the first
    /// behavior outermost, so adding this one last places the check immediately before
    /// the target.
    /// </para>
    /// </remarks>
    public sealed class GuardBehavior : IStuntBehavior
    {
        readonly Func<IMethodInvocation, Exception?> predicate;

        /// <summary>
        /// Initializes the behavior.
        /// </summary>
        /// <param name="predicate">
        /// Returns an exception to fail the call, or <see langword="null"/> to continue.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
        public GuardBehavior(Func<IMethodInvocation, Exception?> predicate)
            => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

        /// <summary>Guards every invocation the pipeline dispatches.</summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Runs the predicate. A <see langword="null"/> result calls <paramref name="next"/>.
        /// An exception fails the invocation without calling <paramref name="next"/>.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            if (invocation == null)
                throw new ArgumentNullException(nameof(invocation));
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            var error = predicate(invocation);
            if (error == null)
                return next(invocation, next);

            var awaitable = AsyncRegistry.AwaitableReturn(invocation.MethodBase);
            if (awaitable == null)
                return invocation.CreateExceptionReturn(error);

            return invocation.CreateValueReturn(AsyncRegistry.Fault(awaitable, error), invocation.Arguments);
        }
    }
}
