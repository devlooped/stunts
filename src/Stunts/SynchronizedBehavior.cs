using System;

namespace Stunts
{
    /// <summary>
    /// An <see cref="IStuntBehavior"/> that serializes overlapping calls on a stunt
    /// using a standard <see cref="object"/> lock.
    /// </summary>
    /// <remarks>
    /// The lock is acquired before <c>next</c> runs and released when it returns,
    /// following the normal <c>lock</c> statement semantics (reentrant).
    /// <para>
    /// By default each behavior owns a private lock object, so one behavior locks
    /// one stunt. Pass an explicit <c>syncRoot</c> to coordinate multiple behaviors
    /// on the same lock.
    /// </para>
    /// <para>
    /// The behavior implements <see cref="ICloneable"/>: the default private lock
    /// is fresh per clone, while an explicit lock object stays shared across clones.
    /// </para>
    /// </remarks>
    public class SynchronizedBehavior : IStuntBehavior, ICloneable
    {
        readonly object syncRoot;
        readonly bool isShared;

        /// <summary>
        /// Initializes the behavior with a private lock owned by the behavior.
        /// </summary>
        public SynchronizedBehavior() : this(new object(), false) { }

        /// <summary>
        /// Initializes the behavior.
        /// </summary>
        /// <param name="syncRoot">
        /// Optional lock object. When <see langword="null"/> (default), the behavior
        /// uses a private object, so one behavior locks one stunt. When provided,
        /// the behavior locks on that object.
        /// </param>
        public SynchronizedBehavior(object? syncRoot)
            : this(syncRoot ?? new object(), syncRoot != null)
        {
        }

        SynchronizedBehavior(object syncRoot, bool isShared)
        {
            this.syncRoot = syncRoot;
            this.isShared = isShared;
        }

        /// <summary>
        /// Returns a new <see cref="SynchronizedBehavior"/>. A default private lock
        /// is fresh per clone; an explicit lock object stays shared.
        /// </summary>
        public object Clone() => new SynchronizedBehavior(
            isShared ? syncRoot : new object(),
            isShared);

        /// <summary>
        /// Applies to every method invocation: overlapping calls on the stunt are
        /// excluded from running concurrently.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Acquires the lock, invokes <paramref name="next"/>, and releases the lock.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            lock (syncRoot)
            {
                return next(invocation, next);
            }
        }
    }
}
