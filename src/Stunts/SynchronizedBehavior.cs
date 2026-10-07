using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// An <see cref="IStuntBehavior"/> that serializes overlapping calls on a stunt
    /// with a non-reentrant lock.
    /// </summary>
    /// <remarks>
    /// The lock is acquired before <c>next</c> runs and released when the attempt
    /// finishes. For methods returning <see cref="Task"/> or <see cref="ValueTask"/>,
    /// the lock is held until the returned task completes, not just until
    /// <c>next</c> returns.
    /// <para>
    /// The lock is non-reentrant: a second call on the same thread while the lock
    /// is held throws <see cref="InvalidOperationException"/> instead of deadlocking.
    /// Constructor logic and the target must therefore not call back into a member
    /// locked by this behavior.
    /// </para>
    /// <para>
    /// By default each behavior owns a private lock, so one behavior locks one
    /// stunt. Pass an explicit <c>syncRoot</c> to coordinate multiple behaviors
    /// or stunts on the same lock (lock identity is what matters).
    /// </para>
    /// <para>
    /// The behavior implements <see cref="ICloneable"/>: the default private lock
    /// is fresh per clone, while an explicit shared lock stays shared across clones.
    /// </para>
    /// </remarks>
    public class SynchronizedBehavior : IStuntBehavior, ICloneable
    {
        static readonly ConditionalWeakTable<object, NonReentrantLock> sharedLocks = new();

        readonly NonReentrantLock syncLock;
        readonly bool isShared;

        /// <summary>
        /// Initializes the behavior with a private lock owned by the behavior.
        /// </summary>
        public SynchronizedBehavior() : this(new NonReentrantLock(new object()), false) { }

        /// <summary>
        /// Initializes the behavior.
        /// </summary>
        /// <param name="syncRoot">
        /// Optional lock object. When <see langword="null"/> (default), the behavior
        /// uses a private object, so one behavior locks one stunt. When provided,
        /// all behaviors constructed with the same object coordinate on the same lock.
        /// </param>
        public SynchronizedBehavior(object? syncRoot)
            : this(syncRoot == null
                ? new NonReentrantLock(new object())
                : sharedLocks.GetValue(syncRoot, key => new NonReentrantLock(key)),
                syncRoot != null)
        {
        }

        SynchronizedBehavior(NonReentrantLock syncLock, bool isShared)
        {
            this.syncLock = syncLock;
            this.isShared = isShared;
        }

        /// <summary>
        /// Returns a new <see cref="SynchronizedBehavior"/>. A default private lock
        /// is fresh per clone; an explicit shared lock stays shared.
        /// </summary>
        public object Clone() => new SynchronizedBehavior(
            isShared ? syncLock : new NonReentrantLock(new object()),
            isShared);

        /// <summary>
        /// Applies to every method invocation: overlapping calls on the stunt are
        /// excluded from running concurrently.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Acquires the lock, invokes <paramref name="next"/>, and releases the lock
        /// when the attempt finishes (when the returned task completes, for
        /// <see cref="Task"/> and <see cref="ValueTask"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The current thread already holds the lock (re-entrant call).
        /// </exception>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            syncLock.Enter();
            bool release = true;
            try
            {
                var result = next(invocation, next);
                if (result.Exception != null)
                    return result;

                // Async methods cannot have ref/out parameters, so Outputs is
                // already final; only the ReturnValue needs wrapping.
                // The thread is freed when we go async, so drop ownership
                // (a later call from this thread waits instead of throwing).
                if (result.ReturnValue is Task task)
                {
                    release = false;
                    syncLock.ReleaseOwnership();
                    task.ContinueWith(_ => syncLock.Exit(), TaskScheduler.Default);
                    return result;
                }

                if (result.ReturnValue is ValueTask valueTask)
                {
                    release = false;
                    syncLock.ReleaseOwnership();
                    var wrapped = ReleaseOnCompletion(valueTask.AsTask());
                    return new MethodReturn(invocation, new ValueTask(wrapped), invocation.Arguments);
                }

                var returnType = result.ReturnValue?.GetType();
                if (returnType != null && returnType.IsGenericType &&
                    returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    release = false;
                    syncLock.ReleaseOwnership();
                    var wrapped = ReleaseOnCompletionGeneric(returnType, result.ReturnValue!);
                    return new MethodReturn(invocation, wrapped, invocation.Arguments);
                }

                return result;
            }
            finally
            {
                if (release)
                    syncLock.Exit();
            }
        }

        async Task ReleaseOnCompletion(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            finally
            {
                syncLock.Exit();
            }
        }

        object ReleaseOnCompletionGeneric(Type valueTaskType, object valueTask)
        {
            var resultType = valueTaskType.GetGenericArguments()[0];
            var asTask = valueTaskType.GetMethod(nameof(ValueTask<int>.AsTask))!;
            var task = (Task)asTask.Invoke(valueTask, null)!;
            var wrap = typeof(SynchronizedBehavior)
                .GetMethod(nameof(WrapGeneric), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var wrappedTask = wrap.MakeGenericMethod(resultType).Invoke(this, new[] { task })!;
            return Activator.CreateInstance(typeof(ValueTask<>).MakeGenericType(resultType), wrappedTask)!;
        }

        async Task<T> WrapGeneric<T>(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
                return ((Task<T>)task).Result;
            }
            finally
            {
                syncLock.Exit();
            }
        }

        /// <summary>
        /// A non-reentrant mutex. Unlike <see cref="Monitor"/>, a second
        /// <see cref="Enter"/> on the owning thread throws instead of succeeding.
        /// <see cref="Exit"/> may run on a different thread (async continuations).
        /// For async, <see cref="ReleaseOwnership"/> drops the thread association
        /// while keeping the lock held, so a later call from the same thread waits
        /// instead of throwing (the thread is not blocked).
        /// </summary>
        sealed class NonReentrantLock
        {
            readonly object mutex;
            bool isLocked;
            int ownerThreadId = -1;

            public NonReentrantLock(object mutex) => this.mutex = mutex;

            public void Enter()
            {
                var current = Environment.CurrentManagedThreadId;
                lock (mutex)
                {
                    if (isLocked && ownerThreadId == current)
                        throw new InvalidOperationException(
                            "Re-entrant call detected: the current thread already holds the synchronized lock. " +
                            "Constructor logic and the target must not call back into a member locked by this behavior.");
                    while (isLocked)
                        Monitor.Wait(mutex);
                    isLocked = true;
                    ownerThreadId = current;
                }
            }

            /// <summary>
            /// Drops the thread ownership while keeping the lock held (async path).
            /// </summary>
            public void ReleaseOwnership()
            {
                lock (mutex)
                {
                    ownerThreadId = -1;
                }
            }

            public void Exit()
            {
                lock (mutex)
                {
                    isLocked = false;
                    ownerThreadId = -1;
                    Monitor.Pulse(mutex);
                }
            }
        }
    }
}
