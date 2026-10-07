#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Threading;
using System.Threading.Tasks;
using Stunts;
using Xunit;

namespace Stunts.Scenarios.Synchronized
{
    public interface IWorker
    {
        int Work(int id);
        Task<int> WorkAsync(int id);
        ValueTask<int> WorkValueAsync(int id);
        int Reentrant();
    }

    /// <summary>
    /// A synchronized behavior serializes overlapping calls with a non-reentrant lock.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            OverlappingSyncCalls_RunOneAtATime();
            OverlappingAsyncCalls_RunOneAtATime();
            LockReleased_WhenTaskCompletes_LaterCallEnters();
            ReentrantCall_ThrowsInsteadOfHanging();
            SharedLock_CoordinatesTwoStunts();
            DefaultLock_DoesNotCoordinateTwoStunts();
            ValueTask_ReleasesLockOnCompletion();
        }

        public void OverlappingSyncCalls_RunOneAtATime()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            var work = new TrackingWork();
            stunt.AddBehavior(work);
            IWorker worker = stunt.ToObject();

            var barrier = new Barrier(2);
            var tasks = new Task[2];
            for (int i = 0; i < 2; i++)
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    worker.Work(1);
                });

            Task.WaitAll(tasks);

            Assert.Equal(1, work.MaxConcurrent);
            Assert.Equal(2, work.Calls);
        }

        public void OverlappingAsyncCalls_RunOneAtATime()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            var work = new TrackingAsyncWork();
            stunt.AddBehavior(work);
            IWorker worker = stunt.ToObject();

            // Start both from background threads so neither blocks the test thread.
            var barrier = new Barrier(2);
            var t1 = Task.Run(async () => { barrier.SignalAndWait(5000); return await worker.WorkAsync(1); });
            var t2 = Task.Run(async () => { barrier.SignalAndWait(5000); return await worker.WorkAsync(2); });

            // Let both start; the second must wait for the first's task.
            work.FirstStarted.Wait(5000);
            Thread.Sleep(200);
            Assert.Equal(1, work.CurrentConcurrent);

            work.Release();
            Task.WaitAll(t1, t2);

            Assert.Equal(1, work.MaxConcurrent);
            Assert.Equal(2, work.Calls);
        }

        public void LockReleased_WhenTaskCompletes_LaterCallEnters()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            var work = new TrackingAsyncWork();
            stunt.AddBehavior(work);
            IWorker worker = stunt.ToObject();

            var t1 = worker.WorkAsync(1);
            work.FirstStarted.Wait(5000);

            // Complete the first task; the lock must be released.
            work.Release();
            t1.Wait(5000);

            // A later call enters immediately (no deadlock).
            var t2 = worker.WorkAsync(2);
            work.SecondStarted.Wait(5000);
            work.Release();
            t2.Wait(5000);

            Assert.Equal(2, work.Calls);
        }

        public void ReentrantCall_ThrowsInsteadOfHanging()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            IWorker? worker = null;
            stunt.AddBehavior(new ReentrantWork(v => worker!.Work(1)));
            worker = stunt.ToObject();

            var ex = Assert.Throws<InvalidOperationException>(() => worker.Reentrant());
            Assert.Contains("Re-entrant", ex.Message);
        }

        public void SharedLock_CoordinatesTwoStunts()
        {
            var syncRoot = new object();
            var barrier = new Barrier(2);
            int concurrent = 0;
            int maxConcurrent = 0;

            IWorker Make()
            {
                var stunt = Stunt.For<IWorker>();
                stunt.AddBehavior(new SynchronizedBehavior(syncRoot));
                stunt.AddBehavior(new LambdaWork(invocation =>
                {
                    var c = Interlocked.Increment(ref concurrent);
                    int max;
                    do { max = maxConcurrent; }
                    while (c > max && Interlocked.CompareExchange(ref maxConcurrent, c, max) != max);
                    try
                    {
                        barrier.SignalAndWait(5000);
                        Thread.Sleep(50);
                        return new MethodReturn(invocation, 0, invocation.Arguments);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref concurrent);
                    }
                }));
                return stunt.ToObject();
            }

            var w1 = Make();
            var w2 = Make();

            var t1 = Task.Run(() => w1.Work(1));
            var t2 = Task.Run(() => w2.Work(2));
            Task.WaitAll(t1, t2);

            Assert.Equal(1, maxConcurrent);
        }

        public void DefaultLock_DoesNotCoordinateTwoStunts()
        {
            var barrier = new Barrier(2);
            int concurrent = 0;
            int maxConcurrent = 0;

            IWorker Make()
            {
                var stunt = Stunt.For<IWorker>();
                stunt.AddBehavior(new SynchronizedBehavior());
                stunt.AddBehavior(new LambdaWork(invocation =>
                {
                    var c = Interlocked.Increment(ref concurrent);
                    int max;
                    do { max = maxConcurrent; }
                    while (c > max && Interlocked.CompareExchange(ref maxConcurrent, c, max) != max);
                    try
                    {
                        barrier.SignalAndWait(5000);
                        Thread.Sleep(50);
                        return new MethodReturn(invocation, 0, invocation.Arguments);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref concurrent);
                    }
                }));
                return stunt.ToObject();
            }

            var w1 = Make();
            var w2 = Make();

            var t1 = Task.Run(() => w1.Work(1));
            var t2 = Task.Run(() => w2.Work(2));
            Task.WaitAll(t1, t2);

            Assert.Equal(2, maxConcurrent);
        }

        public void ValueTask_ReleasesLockOnCompletion()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            var tcs = new TaskCompletionSource<int>();
            int calls = 0;
            stunt.AddBehavior(new LambdaWork(invocation =>
            {
                if (invocation.MethodBase.Name == nameof(IWorker.WorkValueAsync))
                {
                    Interlocked.Increment(ref calls);
                    return new MethodReturn(invocation, new ValueTask<int>(tcs.Task), invocation.Arguments);
                }
                return new MethodReturn(invocation, 0, invocation.Arguments);
            }));
            IWorker worker = stunt.ToObject();

            var vt1 = worker.WorkValueAsync(1);
            Thread.Sleep(100); // Let it start and hold the lock.

            var entered = false;
            var t2 = Task.Run(() =>
            {
                var vt = worker.WorkValueAsync(2); // Blocks on the lock.
                entered = true;
                return vt.AsTask().Result;
            });

            Thread.Sleep(200); // Let t2 block on the lock.
            Assert.False(entered);

            tcs.TrySetResult(42); // Completes vt1's task -> lock released.
            t2.Wait(5000); // t2 must now proceed.
            Assert.True(entered);
            Assert.Equal(2, calls);
            Assert.Equal(42, vt1.AsTask().Result);
            Assert.Equal(42, t2.Result);
        }

        class TrackingWork : IStuntBehavior
        {
            public int Calls;
            public int MaxConcurrent;
            int current;

            public bool AppliesTo(IMethodInvocation invocation)
                => invocation.MethodBase.Name == nameof(IWorker.Work);

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                var c = Interlocked.Increment(ref current);
                int max;
                do { max = MaxConcurrent; }
                while (c > max && Interlocked.CompareExchange(ref MaxConcurrent, c, max) != max);
                try
                {
                    Interlocked.Increment(ref Calls);
                    Thread.Sleep(50);
                    return new MethodReturn(invocation, 0, invocation.Arguments);
                }
                finally
                {
                    Interlocked.Decrement(ref current);
                }
            }
        }

        class TrackingAsyncWork : IStuntBehavior
        {
            public int Calls;
            public int MaxConcurrent;
            public int CurrentConcurrent;
            public readonly ManualResetEventSlim FirstStarted = new();
            public readonly ManualResetEventSlim SecondStarted = new();
            readonly TaskCompletionSource<int> tcs = new();
            int current;

            public bool AppliesTo(IMethodInvocation invocation)
                => invocation.MethodBase.Name == nameof(IWorker.WorkAsync);

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                var call = Interlocked.Increment(ref Calls);
                if (call == 1) FirstStarted.Set();
                else SecondStarted.Set();

                var c = Interlocked.Increment(ref current);
                Interlocked.Exchange(ref CurrentConcurrent, c);
                int max;
                do { max = MaxConcurrent; }
                while (c > max && Interlocked.CompareExchange(ref MaxConcurrent, c, max) != max);

                var task = tcs.Task.ContinueWith(t =>
                {
                    Interlocked.Decrement(ref current);
                    return t.Result;
                });
                return new MethodReturn(invocation, task, invocation.Arguments);
            }

            public void Release() => tcs.TrySetResult(42);
        }

        class ReentrantWork : IStuntBehavior
        {
            readonly Action<IMethodInvocation> reenter;

            public ReentrantWork(Action<IMethodInvocation> reenter) => this.reenter = reenter;

            public bool AppliesTo(IMethodInvocation invocation)
                => invocation.MethodBase.Name == nameof(IWorker.Reentrant);

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                reenter(invocation);
                return new MethodReturn(invocation, 0, invocation.Arguments);
            }
        }

        class LambdaWork : IStuntBehavior
        {
            readonly Func<IMethodInvocation, IMethodReturn> handler;

            public LambdaWork(Func<IMethodInvocation, IMethodReturn> handler) => this.handler = handler;

            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                => handler(invocation);
        }
    }
}
