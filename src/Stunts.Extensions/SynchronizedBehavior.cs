using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// Serializes overlapping calls on a stunt with a lock that any thread can release.
    /// </summary>
    /// <remarks>
    /// Add this behavior first. The pipeline runs the first behavior outermost, so the lock covers
    /// every behavior added after it.
    /// <para>
    /// The lock is acquired before the rest of the pipeline runs and released when that attempt
    /// finishes. A method that returns <see cref="Task"/> or <see cref="ValueTask"/> keeps the lock
    /// until the awaitable settles; the caller resumes after the release.
    /// </para>
    /// <para>
    /// The thread that acquired the lock throws <see cref="InvalidOperationException"/> if it enters
    /// again while the lock is held. Any other thread waits. Release runs on the thread that observes
    /// completion, which may be a thread-pool thread after the awaitable settles.
    /// </para>
    /// <para>
    /// Constructor logic and the target must not call back into a member guarded by this behavior.
    /// A callback made after the first await of an asynchronous member, from a thread other than the
    /// one that entered, waits. When the original call cannot finish without that callback, the wait
    /// deadlocks.
    /// </para>
    /// <para>
    /// The parameterless constructor gives the behavior a private lock, so one behavior locks one stunt.
    /// Passing an object shares one lock across every behavior constructed with that instance.
    /// The object is an identity key and is never locked.
    /// </para>
    /// <para>
    /// <c>StuntBuilder</c> clones <see cref="ICloneable"/> registrations per stunt. A private lock is
    /// fresh on each clone. An explicit shared lock stays shared.
    /// </para>
    /// </remarks>
    public sealed class SynchronizedBehavior : IStuntBehavior, ICloneable
    {
        static readonly ConditionalWeakTable<object, Gate> sharedGates = new();

        readonly Gate gate;
        readonly bool shared;

        /// <summary>
        /// Initializes the behavior with a private lock.
        /// </summary>
        public SynchronizedBehavior()
            : this(new Gate(), shared: false)
        {
        }

        /// <summary>
        /// Initializes the behavior with a lock shared by every behavior constructed with <paramref name="syncRoot"/>.
        /// </summary>
        /// <param name="syncRoot">Object whose identity selects the lock.</param>
        /// <exception cref="ArgumentNullException"><paramref name="syncRoot"/> is <see langword="null"/>.</exception>
        public SynchronizedBehavior(object syncRoot)
            : this(GateFor(syncRoot), shared: true)
        {
        }

        static Gate GateFor(object syncRoot)
        {
            ArgumentNullException.ThrowIfNull(syncRoot);
            return sharedGates.GetValue(syncRoot, static _ => new Gate());
        }

        SynchronizedBehavior(Gate gate, bool shared)
        {
            this.gate = gate;
            this.shared = shared;
        }

        /// <summary>
        /// Returns a behavior with the same sharing. A private lock is new; a shared lock stays shared.
        /// </summary>
        public SynchronizedBehavior Clone()
            => new(shared ? gate : new Gate(), shared);

        object ICloneable.Clone() => Clone();

        /// <summary>Synchronizes every invocation the pipeline dispatches.</summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Acquires the lock, runs <paramref name="next"/>, and releases the lock when the attempt
        /// finishes. Awaitable members release when the returned task settles.
        /// </summary>
        /// <exception cref="InvalidOperationException">The current thread already holds the lock.</exception>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            gate.Enter();
            var released = false;
            try
            {
                return invocation.Proceed(next, (outcome, again) =>
                {
                    Release();
                    return new ValueTask<ProceedOutcome>(outcome);
                });
            }
            catch
            {
                Release();
                throw;
            }

            void Release()
            {
                if (released)
                    return;

                released = true;
                gate.Exit();
            }
        }

        /// <summary>
        /// A semaphore acquired by one thread and released by whichever thread observes completion.
        /// <see cref="Monitor"/> and <see cref="Lock"/> cannot do that: both must be exited by the
        /// thread that entered.
        /// </summary>
        sealed class Gate
        {
            readonly SemaphoreSlim semaphore = new(1, 1);
            int owner;

            public void Enter()
            {
                var thread = Environment.CurrentManagedThreadId;
                if (Volatile.Read(ref owner) == thread)
                    throw new InvalidOperationException(ThisAssembly.Strings.ReentrantCall);

                semaphore.Wait();
                Volatile.Write(ref owner, thread);
            }

            public void Exit()
            {
                Volatile.Write(ref owner, 0);
                semaphore.Release();
            }
        }
    }
}
