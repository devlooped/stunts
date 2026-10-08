using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    public class ExceptionMappingBehaviorTests
    {
        [Fact]
        public void SyncTargetException_BecomesTheMappedException_AndKeepsTheOriginalInner()
        {
            var disk = new IOException("disk");
            Exception? seen = null;
            var pipeline = Pipeline(new ExceptionMappingBehavior(exception =>
            {
                seen = exception;
                return new InvalidOperationException("mapped", exception);
            }));

            var thrown = Assert.Throws<InvalidOperationException>(() => pipeline.Execute(Call(nameof(IStore.Read), (target, call) => throw disk)));

            Assert.Equal("mapped", thrown.Message);
            Assert.Same(disk, seen);
            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public async Task FaultedTask_ThrowsTheMappedException()
        {
            var disk = new IOException("disk");
            var pipeline = Pipeline(new ExceptionMappingBehavior(Wrap));

            var task = pipeline.Execute<Task<int>>(Call(nameof(IStore.CountAsync), (target, call) => call.CreateValueReturn(Task.FromException<int>(disk))));
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);

            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public async Task FaultedValueTask_ThrowsTheMappedException()
        {
            var disk = new IOException("disk");
            var pipeline = Pipeline(new ExceptionMappingBehavior(Wrap));

            var task = pipeline.Execute<ValueTask<int>>(Call(nameof(IStore.MeasureAsync), (target, call) => call.CreateValueReturn(ValueTask.FromException<int>(disk))));
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task.AsTask());

            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public async Task TaskFault_LeavesMethodReturnExceptionNull()
        {
            var pipeline = Pipeline(new ExceptionMappingBehavior(Wrap));

            var result = pipeline.Invoke(Call(nameof(IStore.CountAsync), (target, call) =>
                call.CreateValueReturn(Task.FromException<int>(new IOException("disk")))), true);

            Assert.Null(result.Exception);
            var task = Assert.IsType<Task<int>>(result.ReturnValue);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        [Fact]
        public async Task ValueTaskFault_LeavesMethodReturnExceptionNull()
        {
            var pipeline = Pipeline(new ExceptionMappingBehavior(Wrap));

            var result = pipeline.Invoke(Call(nameof(IStore.MeasureAsync), (target, call) =>
                call.CreateValueReturn(ValueTask.FromException<int>(new IOException("disk")))), true);

            Assert.Null(result.Exception);
            var task = Assert.IsType<ValueTask<int>>(result.ReturnValue);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.AsTask());
        }

        [Fact]
        public void SwallowOff_NullMapCannotTurnAFailureIntoAValue()
        {
            var disk = new IOException("disk");
            var pipeline = Pipeline(new ExceptionMappingBehavior(static _ => null));

            var thrown = Assert.Throws<IOException>(() => pipeline.Execute(Call(nameof(IStore.Read), (target, call) => throw disk)));

            Assert.Same(disk, thrown);
            Assert.Throws<IOException>(() => pipeline.Execute(Call(nameof(IStore.Length), (target, call) => throw new IOException("disk"))));
        }

        [Fact]
        public void SwallowOn_NullMapReturnsTheDefault()
        {
            var pipeline = Pipeline(new ExceptionMappingBehavior(static _ => null, swallow: true));

            Assert.Null(pipeline.Execute<string>(Fail(nameof(IStore.Read))));
            Assert.Equal(0, pipeline.Execute<int>(Fail(nameof(IStore.Length))));
            Assert.Empty(pipeline.Execute<int[]>(Fail(nameof(IStore.Items))));
            pipeline.Execute(Fail(nameof(IStore.Run)));
        }

        [Fact]
        public async Task SwallowOn_UsesTheSuppliedDefaultProvider()
        {
            var defaults = new DefaultValueProvider();
            defaults.Register<int>(() => 4);
            var pipeline = Pipeline(new ExceptionMappingBehavior(static _ => null, swallow: true, defaults: defaults));

            Assert.Equal(4, pipeline.Execute<int>(Fail(nameof(IStore.Length))));
            Assert.Equal(4, await pipeline.Execute<Task<int>>(Fail(nameof(IStore.CountAsync))));
        }

        [Fact]
        public async Task SwallowOn_FaultedTaskReturnsTheDefault()
        {
            var pipeline = Pipeline(new ExceptionMappingBehavior(static _ => null, swallow: true));

            Assert.Equal(0, await pipeline.Execute<Task<int>>(Fail(nameof(IStore.CountAsync))));
            Assert.Equal(0, await pipeline.Execute<ValueTask<int>>(Fail(nameof(IStore.MeasureAsync))));
            await pipeline.Execute<Task>(Fail(nameof(IStore.RunAsync)));
        }

        [Fact]
        public void SwallowOn_MappedExceptionStillFails()
        {
            var disk = new IOException("disk");
            var pipeline = Pipeline(new ExceptionMappingBehavior(Wrap, swallow: true));

            var thrown = Assert.Throws<InvalidOperationException>(() => pipeline.Execute(Call(nameof(IStore.Read), (target, call) => throw disk)));

            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public void ReturnedException_IsTranslated()
        {
            var disk = new IOException("returned");
            var pipeline = Pipeline(
                new ExceptionMappingBehavior(Wrap),
                new Script((invocation, next) => invocation.CreateExceptionReturn(disk)));

            var thrown = Assert.Throws<InvalidOperationException>(() => pipeline.Execute(Call(nameof(IStore.Read), Succeed("ok"))));

            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public void InnerBehaviorException_IsTranslated()
        {
            var disk = new IOException("inner");
            var pipeline = Pipeline(
                new ExceptionMappingBehavior(Wrap),
                new Script((invocation, next) => throw disk));

            var thrown = Assert.Throws<InvalidOperationException>(() => pipeline.Execute(Call(nameof(IStore.Read), Succeed("ok"))));

            Assert.Same(disk, thrown.InnerException);
        }

        [Fact]
        public void OuterBehaviorException_IsLeftAlone()
        {
            var calls = 0;
            var pipeline = Pipeline(
                new Script((invocation, next) => throw new InvalidOperationException("outer")),
                new ExceptionMappingBehavior(exception =>
                {
                    calls++;
                    return Wrap(exception);
                }));

            var thrown = Assert.Throws<InvalidOperationException>(() => pipeline.Execute(Call(nameof(IStore.Read), Succeed("ok"))));

            Assert.Equal("outer", thrown.Message);
            Assert.Null(thrown.InnerException);
            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task SuccessfulCall_DoesNotCallTheMap()
        {
            var calls = 0;
            var pipeline = Pipeline(new ExceptionMappingBehavior(exception =>
            {
                calls++;
                return Wrap(exception);
            }));

            Assert.Equal("ok", pipeline.Execute<string>(Call(nameof(IStore.Read), Succeed("ok"))));
            Assert.Equal(7, pipeline.Execute<int>(Call(nameof(IStore.Length), (target, call) => call.CreateValueReturn(7))));
            Assert.Equal(1, await pipeline.Execute<Task<int>>(Call(nameof(IStore.CountAsync), (target, call) => call.CreateValueReturn(Task.FromResult(1)))));
            Assert.Equal(0, calls);
        }

        [Fact]
        public void SameInstance_IsRethrown()
        {
            var disk = new IOException("disk");
            var pipeline = Pipeline(new ExceptionMappingBehavior(static exception => exception));

            Assert.Same(disk, Assert.Throws<IOException>(() => pipeline.Execute(Call(nameof(IStore.Read), (target, call) => throw disk))));
        }

        [Fact]
        public void NullMapThrows()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ExceptionMappingBehavior(null!));

            Assert.Equal("map", exception.ParamName);
        }

        static Exception Wrap(Exception exception)
            => exception is IOException ? new InvalidOperationException("mapped", exception) : exception;

        static BehaviorPipeline Pipeline(params IStuntBehavior[] behaviors) => new(behaviors);

        static IMethodInvocation Call(string name, MethodInvoker implementation)
            => MethodInvocation.Create(new object(), typeof(IStore).GetMethod(name)!, implementation);

        static IMethodInvocation Fail(string name)
            => Call(name, (target, call) => throw new IOException("disk"));

        static MethodInvoker Succeed(string value)
            => (target, call) => call.CreateValueReturn(value);

        interface IStore
        {
            string Read();

            int Length();

            void Run();

            int[] Items();

            Task<int> CountAsync();

            ValueTask<int> MeasureAsync();

            Task RunAsync();
        }

        sealed class Script(ExecuteHandler execute) : IStuntBehavior
        {
            public bool AppliesTo(IMethodInvocation invocation) => true;

            public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next) => execute(invocation, next);
        }
    }
}
