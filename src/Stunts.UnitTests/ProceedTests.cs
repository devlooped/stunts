using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    public class ProceedTests
    {
        [Fact]
        public void SynchronousValuePassesThrough()
        {
            var result = Proceed(nameof(ISample.Read), (invocation, next) => invocation.CreateValueReturn(3), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            Assert.Equal(3, result.ReturnValue);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void SynchronousCallbackCanReplaceTheValue()
        {
            var result = Proceed(nameof(ISample.Read), (invocation, next) => invocation.CreateValueReturn(2), (outcome, again) =>
                new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue((int)outcome.Value! + 1, outcome.Elapsed)));

            Assert.Equal(3, result.ReturnValue);
        }

        [Fact]
        public void SynchronousThrowBecomesTheOutcome()
        {
            var thrown = new InvalidOperationException("x");
            var seen = default(Exception);

            var result = Proceed(nameof(ISample.Read), (invocation, next) => throw thrown, (outcome, again) =>
            {
                seen = outcome.Exception;
                return new ValueTask<ProceedOutcome>(outcome);
            });

            Assert.Same(thrown, seen);
            Assert.Same(thrown, result.Exception);
        }

        [Fact]
        public void SynchronousCallbackCanRecoverAnException()
        {
            var result = Proceed(nameof(ISample.Read), (invocation, next) => throw new InvalidOperationException(), (outcome, again) =>
                new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue(4, outcome.Elapsed)));

            Assert.Null(result.Exception);
            Assert.Equal(4, result.ReturnValue);
        }

        [Fact]
        public async Task SynchronousAgainRetries()
        {
            var calls = 0;

            var result = Proceed(nameof(ISample.Read), (invocation, next) => invocation.CreateValueReturn(calls++), async (outcome, again) =>
            {
                while ((int)outcome.Value! < 2)
                    outcome = await again();
                return outcome;
            });

            Assert.Equal(2, result.ReturnValue);
            Assert.Equal(3, calls);
        }

        [Fact]
        public void SynchronousCallbackThatDoesNotFinishThrows()
        {
            var pending = new TaskCompletionSource<ProceedOutcome>();
            var exception = Assert.Throws<InvalidOperationException>(() =>
                Proceed(nameof(ISample.Read), (invocation, next) => invocation.CreateValueReturn(1), (outcome, again) =>
                    new ValueTask<ProceedOutcome>(pending.Task)));

            Assert.Contains(nameof(ISample.Read), exception.Message);
        }

        [Fact]
        public void RefOutputIsPreserved()
        {
            var method = Sample(nameof(ISample.Inc));
            var invocation = MethodInvocation.Create(new object(), method, (invocation, next) =>
            {
                var value = (int)invocation.Arguments.GetValue("value")!;
                return invocation.CreateValueReturn(null, invocation.Arguments.SetValue("value", value + 1));
            }, 1);

            var result = invocation.Proceed((call, next) => call.CreateInvokeReturn(), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            Assert.Equal(2, result.Outputs.GetValue("value"));
        }

        [Fact]
        public async Task TaskResultIsUnwrappedAndCanBeReplaced()
        {
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) => invocation.CreateValueReturn(Task.FromResult(3)), (outcome, again) =>
                new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue((int)outcome.Value! + 1, outcome.Elapsed)));

            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.Null(result.Exception);
            Assert.Equal(4, await task);
        }

        [Fact]
        public async Task FaultedTaskKeepsTheOriginalException()
        {
            var original = new InvalidOperationException("boom");
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) => invocation.CreateValueReturn(Task.FromException<int>(original)), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.Null(result.Exception);
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Same(original, actual);
        }

        [Fact]
        public async Task CanceledTaskStaysCanceled()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) =>
                invocation.CreateValueReturn(Task.FromCanceled<int>(source.Token)), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.True(task.IsCanceled);
            var actual = await Assert.ThrowsAsync<TaskCanceledException>(() => task);
            Assert.Equal(source.Token, actual.CancellationToken);
        }

        [Fact]
        public async Task NullTaskIsASynchronousFailure()
        {
            Task<int>? missing = null;
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) => invocation.CreateValueReturn(missing), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            var exception = Assert.IsType<NullReferenceException>(result.Exception);
            Assert.Contains(nameof(ISample.GetAsync), exception.Message);
        }

        [Fact]
        public async Task SynchronousFailureOnATaskMethodCanBeRecovered()
        {
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) => throw new InvalidOperationException("x"), (outcome, again) =>
                new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue(4, outcome.Elapsed)));

            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.Equal(4, await task);
        }

        [Fact]
        public async Task RetryWaitsForTheAttempt()
        {
            var calls = 0;
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) =>
            {
                calls++;
                return calls < 3
                    ? invocation.CreateValueReturn(Task.FromException<int>(new IOException()))
                    : invocation.CreateValueReturn(Task.FromResult(5));
            }, async (outcome, again) =>
            {
                while (outcome.Exception is IOException)
                    outcome = await again();
                return outcome;
            });

            Assert.Equal(5, await Assert.IsType<Task<int>>(result.ReturnValue));
            Assert.Equal(3, calls);
        }

        [Fact]
        public async Task ElapsedIsReportedWhenTheTaskCompletes()
        {
            var gate = new TaskCompletionSource<int>();
            TimeSpan? elapsed = null;
            var result = Proceed(nameof(ISample.GetAsync), (invocation, next) => invocation.CreateValueReturn(gate.Task), async (outcome, again) =>
            {
                elapsed = outcome.Elapsed;
                return outcome;
            });

            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            Assert.False(task.IsCompleted);
            Assert.Null(elapsed);

            gate.SetResult(9);

            Assert.Equal(9, await task);
            Assert.NotNull(elapsed);
        }

        [Fact]
        public async Task NonGenericTaskCompletes()
        {
            var result = Proceed(nameof(ISample.RunAsync), (invocation, next) => invocation.CreateValueReturn(Task.CompletedTask), (outcome, again) =>
            {
                Assert.Null(outcome.Exception);
                Assert.Null(outcome.Value);
                return new ValueTask<ProceedOutcome>(outcome);
            });

            await Assert.IsAssignableFrom<Task>(result.ReturnValue);
        }

        [Fact]
        public async Task ValueTaskIsConsumedOnceAcrossBehaviors()
        {
            var method = Sample(nameof(ISample.ReadAsync));
            var invocation = MethodInvocation.Create(new object(), method, (call, next) => call.CreateValueReturn(new ValueTask<int>(7)));
            var pipeline = new BehaviorPipeline(
                (ExecuteHandler)((call, next) => call.Proceed(next, (outcome, again) =>
                    new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue((int)outcome.Value! + 1, outcome.Elapsed)))),
                (ExecuteHandler)((call, next) => call.Proceed(next, (outcome, again) =>
                    new ValueTask<ProceedOutcome>(outcome))));

            var result = pipeline.Invoke(invocation);

            Assert.Equal(8, await Assert.IsType<ValueTask<int>>(result.ReturnValue));
        }

        [Fact]
        public async Task RegisteredAdapterWrapsAValueTask()
        {
            AsyncRegistry.Register<Guid>();
            var id = Guid.NewGuid();
            var invocation = MethodInvocation.Create(new object(), Sample(nameof(ISample.IdAsync)));
            var result = invocation.Proceed((call, next) => call.CreateValueReturn(new ValueTask<Guid>(id)), (outcome, again) =>
                new ValueTask<ProceedOutcome>(outcome));

            Assert.Equal(id, await Assert.IsType<ValueTask<Guid>>(result.ReturnValue));
        }

        [Fact]
        public void OpenGenericTaskHasNoAdapter()
        {
            var method = Sample(nameof(ISample.Echo));
            var invocation = new MethodInvocation(new object(), method, new ArgumentCollection(method.GetParameters()));
            var exception = Assert.Throws<NotSupportedException>(() =>
                invocation.Proceed((call, next) => call.CreateValueReturn(Task.FromResult(1)), (outcome, again) =>
                    new ValueTask<ProceedOutcome>(outcome)));

            Assert.Contains("AsyncRegistry.Register<", exception.Message);
        }

        [Fact]
        public void RejectsAMissingCallback()
        {
            var invocation = MethodInvocation.Create(new object(), Sample(nameof(ISample.Read)));

            Assert.Throws<ArgumentNullException>(() => invocation.Proceed((call, next) => call.CreateValueReturn(1), null!));
        }

        static IMethodReturn Proceed(string name, ExecuteHandler next, ProceedHandler callback)
        {
            var invocation = MethodInvocation.Create(new object(), Sample(name));
            return invocation.Proceed(next, callback);
        }

        static MethodInfo Sample(string name) => typeof(ISample).GetMethod(name)!;

        interface ISample
        {
            int Read();

            void Inc(ref int value);

            Task RunAsync();

            Task<int> GetAsync();

            ValueTask<int> ReadAsync();

            ValueTask<Guid> IdAsync();

            Task<T> Echo<T>(T value);
        }
    }
}
