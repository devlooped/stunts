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
    /// A synchronized behavior serializes overlapping calls using a standard lock.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            OverlappingSyncCalls_RunOneAtATime();
            ReentrantCall_Succeeds_WithReentrantLock();
            SharedLock_CoordinatesTwoStunts();
            DefaultLock_DoesNotCoordinateTwoStunts();
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

        public void ReentrantCall_Succeeds_WithReentrantLock()
        {
            var stunt = Stunt.For<IWorker>();
            stunt.AddBehavior(new SynchronizedBehavior());
            IWorker? worker = null;
            stunt.AddBehavior(new ReentrantWork(v => worker!.Work(1)));
            stunt.AddBehavior(new LambdaWork(invocation =>
                invocation.MethodBase.Name == nameof(IWorker.Work)
                    ? new MethodReturn(invocation, 0, invocation.Arguments)
                    : new MethodReturn(invocation, 0, invocation.Arguments)));
            worker = stunt.ToObject();

            // Reentrant Monitor: same-thread re-entry succeeds (no deadlock, no throw).
            var result = worker.Reentrant();
            Assert.Equal(0, result);
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
