using System;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    public class GuardBehaviorTests
    {
        [Fact]
        public void NullPredicateThrows()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new GuardBehavior(null!));

            Assert.Equal("predicate", exception.ParamName);
        }

        [Fact]
        public void RejectedCall_DoesNotRunTheTarget_AndTheCallerObservesTheException()
        {
            var error = new ArgumentNullException("name");
            var next = new Counter();
            var target = new Counter();
            var pipeline = Pipeline(
                new GuardBehavior(invocation => invocation.Arguments.GetValue(0) == null ? error : null),
                next.Pass());

            var returned = pipeline.Invoke(Call(nameof(IStore.Read), target.Return("ok"), null), false);
            var thrown = Assert.Throws<ArgumentNullException>(() => pipeline.Execute(Call(nameof(IStore.Read), target.Return("ok"), null)));

            Assert.Same(error, returned.Exception);
            Assert.Same(error, thrown);
            Assert.Equal(0, next.Calls);
            Assert.Equal(0, target.Calls);
        }

        [Fact]
        public void AcceptedCall_RunsTheTargetOnce()
        {
            var next = new Counter();
            var target = new Counter();
            var pipeline = Pipeline(
                new GuardBehavior(invocation => invocation.Arguments.GetValue(0) == null ? new ArgumentNullException("name") : null),
                next.Pass());

            var value = pipeline.Execute<string>(Call(nameof(IStore.Read), target.Return("ok"), "ada"));

            Assert.Equal("ok", value);
            Assert.Equal(1, next.Calls);
            Assert.Equal(1, target.Calls);
        }

        [Fact]
        public void RejectedVoid_ThrowsOnTheCallerThread()
        {
            var error = new ArgumentNullException("name");
            var target = new Counter();
            var pipeline = Pipeline(new GuardBehavior(_ => error));

            var thrown = Assert.Throws<ArgumentNullException>(() =>
                pipeline.Execute(Call(nameof(IStore.Run), target.Return(null), null)));

            Assert.Same(error, thrown);
            Assert.Equal(0, target.Calls);
        }

        [Fact]
        public async Task RejectedAwaitable_FaultsWithThePredicateException_AndSkipsTheTarget()
        {
            await Reject<Task>(nameof(IStore.SaveAsync), static task => task);
            await Reject<Task<int>>(nameof(IStore.CountAsync), static task => task);
            await Reject<ValueTask>(nameof(IStore.FlushAsync), static task => task.AsTask());
            await Reject<ValueTask<int>>(nameof(IStore.MeasureAsync), static task => task.AsTask());
        }

        [Fact]
        public async Task CanceledException_FaultsTheTaskWithThatException()
        {
            var error = new OperationCanceledException("stop");
            var target = new Counter();
            var pipeline = Pipeline(new GuardBehavior(_ => error));

            var result = pipeline.Invoke(Call(nameof(IStore.CountAsync), target.Return(Task.FromResult(1)), null), true);

            Assert.Null(result.Exception);
            Assert.Equal(0, target.Calls);
            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.True(task.IsFaulted);
            Assert.False(task.IsCanceled);
            var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.Same(error, thrown);
        }

        [Fact]
        public async Task AcceptedAwaitable_RunsTheTargetOnce()
        {
            var target = new Counter();
            var task = Task.FromResult(4);
            var pipeline = Pipeline(new GuardBehavior(_ => (Exception?)null));

            var returned = pipeline.Execute<Task<int>>(Call(nameof(IStore.CountAsync), (invocationTarget, call) =>
            {
                target.Calls++;
                return call.CreateValueReturn(task);
            }, "ada"));

            Assert.Same(task, returned);
            Assert.Equal(4, await returned);
            Assert.Equal(1, target.Calls);
        }

        static async Task Reject<T>(string name, Func<T, Task> observe)
        {
            var error = new ArgumentNullException("name");
            var target = new Counter();
            var pipeline = Pipeline(new GuardBehavior(_ => error));

            var result = pipeline.Invoke(Call(name, target.Return(default(T)!), null), true);

            Assert.Null(result.Exception);
            Assert.Equal(0, target.Calls);
            var awaitable = Assert.IsType<T>(result.ReturnValue);
            AssertFaulted(result.ReturnValue);
            var thrown = await Assert.ThrowsAsync<ArgumentNullException>(() => observe(awaitable));
            Assert.Same(error, thrown);
        }

        static void AssertFaulted(object? awaitable)
        {
            switch (awaitable)
            {
                case Task task:
                    Assert.True(task.IsFaulted);
                    Assert.False(task.IsCanceled);
                    break;
                case ValueTask valueTask:
                    Assert.True(valueTask.IsFaulted);
                    Assert.False(valueTask.IsCanceled);
                    break;
                case ValueTask<int> valueTask:
                    Assert.True(valueTask.IsFaulted);
                    Assert.False(valueTask.IsCanceled);
                    break;
                default:
                    throw new InvalidOperationException(awaitable?.GetType().ToString() ?? "null");
            }
        }

        static BehaviorPipeline Pipeline(params IStuntBehavior[] behaviors) => new(behaviors);

        static IMethodInvocation Call(string name, MethodInvoker implementation, string? argument)
            => MethodInvocation.Create(new object(), typeof(IStore).GetMethod(name)!, implementation, argument);

        interface IStore
        {
            string Read(string name);

            void Run(string name);

            Task SaveAsync(string name);

            Task<int> CountAsync(string name);

            ValueTask FlushAsync(string name);

            ValueTask<int> MeasureAsync(string name);
        }

        sealed class Counter
        {
            public int Calls;

            public MethodInvoker Return(object? value)
                => (target, call) =>
                {
                    Calls++;
                    return call.CreateValueReturn(value);
                };

            public IStuntBehavior Pass()
                => new Script((invocation, next) =>
                {
                    Calls++;
                    return next(invocation, next);
                });
        }

        sealed class Script(ExecuteHandler execute) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next) => execute(invocation, next);
        }
    }
}
