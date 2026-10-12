#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.Scenarios.Guards
{
    /// <summary>
    /// A guard predicate rejects a call before the target runs. A null result runs the target once.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            RejectedCall_DoesNotRunTheTarget_AndTheCallerObservesTheException();
            AcceptedCall_RunsTheTargetOnce();
            RejectedTask_FaultsBeforeTheTarget().GetAwaiter().GetResult();
            RejectedValueTask_FaultsBeforeTheTarget().GetAwaiter().GetResult();
        }

        public void RejectedCall_DoesNotRunTheTarget_AndTheCallerObservesTheException()
        {
            var error = new ArgumentNullException("sku");
            var target = new CountingTarget();
            IOrders orders = Create(invocation => invocation.MethodBase.Name == nameof(IOrders.Count) ? error : null, target);

            var thrown = Assert.Throws<ArgumentNullException>(() => orders.Count(null));

            Assert.Same(error, thrown);
            Assert.Equal(0, target.Calls);
        }

        public void AcceptedCall_RunsTheTargetOnce()
        {
            var target = new CountingTarget();
            IOrders orders = Create(_ => null, target);

            Assert.Equal(1, orders.Count("ada"));

            Assert.Equal(1, target.Calls);
        }

        public async Task RejectedTask_FaultsBeforeTheTarget()
        {
            var error = new ArgumentNullException("sku");
            var target = new CountingTarget();
            IOrders orders = Create(invocation => invocation.MethodBase.Name == nameof(IOrders.TrackAsync) ? error : null, target);

            var pending = orders.TrackAsync(null);

            Assert.Equal(0, target.Calls);
            Assert.True(pending.IsFaulted);
            var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => pending);
            Assert.Same(error, thrown);
        }

        public async Task RejectedValueTask_FaultsBeforeTheTarget()
        {
            var error = new ArgumentNullException("sku");
            var target = new CountingTarget();
            IOrders orders = Create(invocation => invocation.MethodBase.Name == nameof(IOrders.MeasureAsync) ? error : null, target);

            var pending = orders.MeasureAsync(null);

            Assert.Equal(0, target.Calls);
            Assert.True(pending.IsFaulted);
            var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => pending.AsTask());
            Assert.Same(error, thrown);
        }

        static IOrders Create(Func<IMethodInvocation, Exception?> predicate, CountingTarget target)
        {
            var stunt = Stunt.For<IOrders>();
            stunt.AddBehavior(new GuardBehavior(predicate));
            stunt.AddBehavior(target);
            return stunt.ToObject();
        }

        sealed class CountingTarget : IStuntBehavior
        {
            public int Calls { get; private set; }

            public bool AppliesTo(IMethodInvocation invocation)
                => invocation.MethodBase is not System.Reflection.ConstructorInfo;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            {
                Calls++;
                return invocation.MethodBase.Name == nameof(IOrders.TrackAsync)
                    ? invocation.CreateValueReturn(Task.FromResult(1))
                    : invocation.MethodBase.Name == nameof(IOrders.MeasureAsync)
                        ? invocation.CreateValueReturn(new ValueTask<int>(1))
                        : invocation.CreateValueReturn(1);
            }
        }
    }

    public interface IOrders
    {
        int Count(string sku);

        Task<int> TrackAsync(string sku);

        ValueTask<int> MeasureAsync(string sku);
    }
}
