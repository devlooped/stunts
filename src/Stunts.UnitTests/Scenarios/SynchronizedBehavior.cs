#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    /// <summary>
    /// A synchronized behavior excludes overlapping calls, holds the lock until an
    /// awaitable settles, and throws when the owning thread re-enters.
    /// </summary>
    public class SynchronizedBehaviorTests : IRunnable
    {
        public void Run()
        {
            OverlappingSyncCalls_RunOneAtATime();
            OverlappingAsyncCalls_RunOneAtATime();
            OverlappingValueTaskCalls_RunOneAtATime();
            CallerResumesAfterTheLockIsReleased();
            ReentrantCall_Throws_ThenALaterCallEnters();
            SameThreadCallWhileAsyncIsInFlight_Throws();
            FaultedAwaitable_PreservesTheException_AndReleases();
            CanceledAwaitable_Releases();
            NullAwaitable_Releases();
            SyncException_Releases();
            SharedLock_BlocksTheOtherStunt();
            DefaultLock_DoesNotBlockTheOtherStunt();
            SyncRootIsNotLocked();
            BuilderClonesPrivateLocks();
            BuilderKeepsAnExplicitSharedLock();
        }

        public void OverlappingSyncCalls_RunOneAtATime()
        {
            var target = new GateTarget();
            var worker = Create(new SynchronizedBehavior(), target);

            Exclude(static worker => Task.FromResult(worker.Work(1)), worker, worker, target);
        }

        public void OverlappingAsyncCalls_RunOneAtATime()
        {
            var target = new GateTarget();
            var worker = Create(new SynchronizedBehavior(), target);

            Exclude(static worker => worker.WorkAsync(1), worker, worker, target);
        }

        public void OverlappingValueTaskCalls_RunOneAtATime()
        {
            var target = new GateTarget();
            var worker = Create(new SynchronizedBehavior(), target);

            Exclude(static worker => worker.WorkValueAsync(1).AsTask(), worker, worker, target);
        }

        public void CallerResumesAfterTheLockIsReleased()
        {
            Resume(
                static worker => worker.WorkAsync(1),
                static (invocation, hold) => invocation.MethodBase.Name == nameof(IWorker.WorkAsync)
                    ? invocation.CreateValueReturn(hold.Task)
                    : invocation.CreateValueReturn(1));
            Resume(
                static worker => worker.WorkValueAsync(1).AsTask(),
                static (invocation, hold) => invocation.MethodBase.Name == nameof(IWorker.WorkValueAsync)
                    ? invocation.CreateValueReturn(new ValueTask<int>(hold.Task))
                    : invocation.CreateValueReturn(1));
        }

        public void ReentrantCall_Throws_ThenALaterCallEnters()
        {
            IWorker? worker = null;
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            stunt.AddBehavior(new Script(invocation =>
            {
                if (invocation.MethodBase.Name == nameof(IWorker.Call))
                    worker!.Work(1);

                return invocation.CreateValueReturn(1);
            }));
            worker = stunt.ToObject();

            var exception = Assert.Throws<InvalidOperationException>(() => worker.Call());
            Assert.Contains("Re-entrant", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, worker.Work(1));
        }

        public void SameThreadCallWhileAsyncIsInFlight_Throws()
        {
            var hold = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = Create(new SynchronizedBehavior(), new Script(invocation =>
                invocation.MethodBase.Name == nameof(IWorker.WorkAsync)
                    ? invocation.CreateValueReturn(hold.Task)
                    : invocation.CreateValueReturn(1)));

            var pending = worker.WorkAsync(1);

            var exception = Assert.Throws<InvalidOperationException>(() => worker.Work(1));
            Assert.Contains("Re-entrant", exception.Message, StringComparison.Ordinal);

            hold.SetResult(4);
            Assert.Equal(4, pending.GetAwaiter().GetResult());
            Assert.Equal(1, worker.Work(1));
        }

        public void FaultedAwaitable_PreservesTheException_AndReleases()
        {
            var boom = new InvalidOperationException("boom");
            var worker = Create(new SynchronizedBehavior(), new Script(invocation =>
                invocation.MethodBase.Name == nameof(IWorker.WorkValueAsync)
                    ? invocation.CreateValueReturn(ValueTask.FromException<int>(boom))
                    : invocation.CreateValueReturn(1)));

            var exception = Assert.Throws<InvalidOperationException>(() => worker.WorkValueAsync(1).GetAwaiter().GetResult());
            Assert.Same(boom, exception);
            Assert.Equal(1, worker.Work(1));
        }

        public void CanceledAwaitable_Releases()
        {
            var worker = Create(new SynchronizedBehavior(), new Script(invocation =>
                invocation.MethodBase.Name == nameof(IWorker.WorkAsync)
                    ? invocation.CreateValueReturn(Task.FromCanceled<int>(new CancellationToken(true)))
                    : invocation.CreateValueReturn(1)));

            Assert.ThrowsAny<OperationCanceledException>(() => worker.WorkAsync(1).GetAwaiter().GetResult());
            Assert.Equal(1, worker.Work(1));
        }

        public void NullAwaitable_Releases()
        {
            var worker = Create(new SynchronizedBehavior(), new Script(invocation =>
                invocation.MethodBase.Name == nameof(IWorker.WorkAsync)
                    ? invocation.CreateValueReturn(default(Task<int>))
                    : invocation.CreateValueReturn(1)));

            Assert.Throws<NullReferenceException>(() => worker.WorkAsync(1).GetAwaiter().GetResult());
            Assert.Equal(1, worker.Work(1));
        }

        public void SyncException_Releases()
        {
            var worker = Create(new SynchronizedBehavior(), new Script(invocation =>
            {
                if (invocation.MethodBase.Name == nameof(IWorker.Call))
                    throw new InvalidOperationException("boom");

                return invocation.CreateValueReturn(1);
            }));

            var exception = Assert.Throws<InvalidOperationException>(() => worker.Call());
            Assert.Equal("boom", exception.Message);
            Assert.Equal(1, worker.Work(1));
        }

        public void SharedLock_BlocksTheOtherStunt()
        {
            var root = new object();
            var target = new GateTarget();
            var first = Create(new SynchronizedBehavior(root), target);
            var second = Create(new SynchronizedBehavior(root), target);

            Exclude(static worker => worker.WorkAsync(1), first, second, target);
        }

        public void DefaultLock_DoesNotBlockTheOtherStunt()
        {
            var target = new GateTarget();
            var first = Create(new SynchronizedBehavior(), target);
            var second = Create(new SynchronizedBehavior(), target);

            Overlap(static worker => worker.WorkAsync(1), first, second, target);
        }

        public void SyncRootIsNotLocked()
        {
            var root = new object();
            var held = new ManualResetEventSlim(false);
            var leave = new ManualResetEventSlim(false);
            var external = Task.Run(() =>
            {
                lock (root)
                {
                    held.Set();
                    Assert.True(leave.Wait(TimeSpan.FromSeconds(5)));
                }
            });
            Assert.True(held.Wait(TimeSpan.FromSeconds(5)));

            var worker = Create(new SynchronizedBehavior(root), new Script(invocation => invocation.CreateValueReturn(1)));
            var call = Task.Run(() => worker.Work(1));
            Assert.True(call.Wait(TimeSpan.FromSeconds(2)), "The call blocked on the caller's sync root.");

            leave.Set();
            Assert.True(external.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, call.Result);
            Assert.Throws<ArgumentNullException>("syncRoot", () => new SynchronizedBehavior(null!));
        }

        public void BuilderClonesPrivateLocks()
        {
            var target = new GateTarget();
            var builder = Stunt.Builder();
            builder.AddBehavior(new SynchronizedBehavior());
            builder.AddBehavior(target);

            Overlap(static worker => worker.WorkAsync(1), builder.Build<IWorker>(), builder.Build<IWorker>(), target);
        }

        public void BuilderKeepsAnExplicitSharedLock()
        {
            var target = new GateTarget();
            var builder = Stunt.Builder();
            builder.AddBehavior(new SynchronizedBehavior(new object()));
            builder.AddBehavior(target);

            Exclude(static worker => worker.WorkAsync(1), builder.Build<IWorker>(), builder.Build<IWorker>(), target);
        }

        static void Resume(Func<IWorker, Task<int>> start, Func<IMethodInvocation, TaskCompletionSource<int>, IMethodReturn> execute)
        {
            var hold = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = Create(new SynchronizedBehavior(), new Script(invocation => execute(invocation, hold)));
            var pending = start(worker);
            hold.SetResult(4);
            Assert.Equal(4, pending.GetAwaiter().GetResult());
            Assert.Equal(1, worker.Work(1));
        }

        static void Exclude(Func<IWorker, Task<int>> call, IWorker firstWorker, IWorker secondWorker, GateTarget target)
        {
            using var start = new Barrier(3);
            var first = Start(start, firstWorker, call);
            var second = Start(start, secondWorker, call);
            Assert.True(start.SignalAndWait(5000));

            Assert.True(target.FirstEntered.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(target.SecondEntered.Task.Wait(TimeSpan.FromMilliseconds(200)));
            Assert.Equal(1, target.Entered);

            target.Release(7);
            Assert.Equal(7, Finish(first));
            Assert.Equal(7, Finish(second));
        }

        static void Overlap(Func<IWorker, Task<int>> call, IWorker firstWorker, IWorker secondWorker, GateTarget target)
        {
            using var start = new Barrier(3);
            var first = Start(start, firstWorker, call);
            var second = Start(start, secondWorker, call);
            Assert.True(start.SignalAndWait(5000));

            Assert.True(target.FirstEntered.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(target.SecondEntered.Task.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, target.Entered);

            target.Release(7);
            Assert.Equal(7, Finish(first));
            Assert.Equal(7, Finish(second));
        }

        static Task<int> Start(Barrier start, IWorker worker, Func<IWorker, Task<int>> call)
            => Task.Run(() =>
            {
                Assert.True(start.SignalAndWait(5000));
                return call(worker);
            });

        static int Finish(Task<int> task)
        {
            Assert.True(task.Wait(TimeSpan.FromSeconds(5)), "Timed out waiting for the call to finish.");
            return task.Result;
        }

        static IWorker Create(IStuntBehavior synchronized, IStuntBehavior inner)
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(synchronized);
            stunt.AddBehavior(inner);
            return stunt.ToObject();
        }

        sealed class GateTarget : IStuntBehavior
        {
            readonly TaskCompletionSource<int> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int entered;

            public int Entered => Volatile.Read(ref entered);

            public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Release(int value) => result.TrySetResult(value);

            public bool AppliesTo(IMethodInvocation invocation)
            {
                var name = invocation.MethodBase.Name;
                return name == nameof(IWorker.Work)
                    || name == nameof(IWorker.WorkAsync)
                    || name == nameof(IWorker.WorkValueAsync);
            }

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                var call = Interlocked.Increment(ref entered);
                (call == 1 ? FirstEntered : SecondEntered).TrySetResult();

                var name = invocation.MethodBase.Name;
                if (name == nameof(IWorker.WorkAsync))
                    return invocation.CreateValueReturn(result.Task);
                if (name == nameof(IWorker.WorkValueAsync))
                    return invocation.CreateValueReturn(new ValueTask<int>(result.Task));

                if (!result.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("The synchronized call was not released.");

                return invocation.CreateValueReturn(result.Task.Result);
            }
        }

        sealed class Script(Func<IMethodInvocation, IMethodReturn> execute) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => execute(invocation);
        }
    }

    public interface IWorker
    {
        int Work(int id);

        int Call();

        Task<int> WorkAsync(int id);

        ValueTask<int> WorkValueAsync(int id);
    }
}
