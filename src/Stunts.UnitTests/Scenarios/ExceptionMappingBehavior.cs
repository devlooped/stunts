#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    /// <summary>
    /// An exception-mapping behavior translates a target failure, keeps a wrapped
    /// original as the inner exception, and leaves swallowing off by default.
    /// </summary>
    public class ExceptionMappingBehaviorTests : IRunnable
    {
        public void Run()
        {
            SyncTargetException_BecomesTheMappedException_AndKeepsTheOriginalInner();
            FaultedTask_ThrowsTheMappedException();
            FaultedValueTask_ThrowsTheMappedException();
            TaskFault_LeavesMethodReturnExceptionNull();
            ValueTaskFault_LeavesMethodReturnExceptionNull();
            SwallowOff_NullMapCannotTurnAFailureIntoAValue();
            SwallowOn_NullMapReturnsTheDefault();
            SwallowOn_FaultedTaskReturnsTheDefault();
            SwallowOn_MappedExceptionStillFails();
            ReturnedException_IsTranslated();
            InnerBehaviorException_IsTranslated();
            OuterBehaviorException_IsLeftAlone();
            SuccessfulCall_DoesNotCallTheMap();
            SameInstance_IsRethrown();
            NullMapThrows();
        }

        public void SyncTargetException_BecomesTheMappedException_AndKeepsTheOriginalInner()
        {
            var disk = new IOException("disk");
            Exception? seen = null;
            var store = Create(new ExceptionMappingBehavior(exception =>
            {
                seen = exception;
                return new InvalidOperationException("mapped", exception);
            }));
            store.Failure = disk;

            var thrown = Assert.Throws<InvalidOperationException>(() => store.Read());

            Assert.Equal("mapped", thrown.Message);
            Assert.Same(disk, seen);
            Assert.Same(disk, thrown.InnerException);
        }

        public void FaultedTask_ThrowsTheMappedException()
        {
            var disk = new IOException("disk");
            var store = Create(new ExceptionMappingBehavior(Wrap));
            store.Failure = disk;

            var thrown = Assert.Throws<InvalidOperationException>(() => store.CountAsync().GetAwaiter().GetResult());

            Assert.Same(disk, thrown.InnerException);
        }

        public void FaultedValueTask_ThrowsTheMappedException()
        {
            var disk = new IOException("disk");
            var store = Create(new ExceptionMappingBehavior(Wrap));
            store.Failure = disk;

            var thrown = Assert.Throws<InvalidOperationException>(() => store.MeasureAsync().GetAwaiter().GetResult());

            Assert.Same(disk, thrown.InnerException);
        }

        public void TaskFault_LeavesMethodReturnExceptionNull()
        {
            var recorded = new RecordingBehavior();
            var store = Create(recorded, new ExceptionMappingBehavior(Wrap));
            store.Failure = new IOException("disk");

            var pending = store.CountAsync();

            var entry = Assert.Single(recorded.Invocations, call => call.Invocation.MethodBase.Name == nameof(Store.CountAsync));
            Assert.Null(entry.Return.Exception);
            Assert.IsType<Task<int>>(entry.Return.ReturnValue);
            Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult());
        }

        public void ValueTaskFault_LeavesMethodReturnExceptionNull()
        {
            var recorded = new RecordingBehavior();
            var store = Create(recorded, new ExceptionMappingBehavior(Wrap));
            store.Failure = new IOException("disk");

            var pending = store.MeasureAsync();

            var entry = Assert.Single(recorded.Invocations, call => call.Invocation.MethodBase.Name == nameof(Store.MeasureAsync));
            Assert.Null(entry.Return.Exception);
            Assert.IsType<ValueTask<int>>(entry.Return.ReturnValue);
            Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult());
        }

        public void SwallowOff_NullMapCannotTurnAFailureIntoAValue()
        {
            var disk = new IOException("disk");
            var store = Create(new ExceptionMappingBehavior(static _ => null));
            store.Failure = disk;

            var thrown = Assert.Throws<IOException>(() => store.Read());

            Assert.Same(disk, thrown);
            store.Failure = new IOException("disk");
            Assert.Throws<IOException>(() => store.Length());
        }

        public void SwallowOn_NullMapReturnsTheDefault()
        {
            var store = Create(new ExceptionMappingBehavior(static _ => null, swallow: true));
            store.Failure = new IOException("disk");

            Assert.Null(store.Read());
            Assert.Equal(0, store.Length());
            Assert.Empty(store.Items());
            store.Run();
        }

        public void SwallowOn_FaultedTaskReturnsTheDefault()
        {
            var store = Create(new ExceptionMappingBehavior(static _ => null, swallow: true));
            store.Failure = new IOException("disk");

            Assert.Equal(0, store.CountAsync().GetAwaiter().GetResult());
            Assert.Equal(0, store.MeasureAsync().GetAwaiter().GetResult());
            store.RunAsync().GetAwaiter().GetResult();
        }

        public void SwallowOn_MappedExceptionStillFails()
        {
            var disk = new IOException("disk");
            var store = Create(new ExceptionMappingBehavior(Wrap, swallow: true));
            store.Failure = disk;

            var thrown = Assert.Throws<InvalidOperationException>(() => store.Read());

            Assert.Same(disk, thrown.InnerException);
        }

        public void ReturnedException_IsTranslated()
        {
            var disk = new IOException("returned");
            var stunt = Stunt.For<Store>();
            stunt.AddBehavior(new ExceptionMappingBehavior(Wrap));
            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase is ConstructorInfo
                    ? next(invocation, next)
                    : invocation.CreateExceptionReturn(disk));
            var store = stunt.ToObject();

            var thrown = Assert.Throws<InvalidOperationException>(() => store.Read());

            Assert.Same(disk, thrown.InnerException);
        }

        public void InnerBehaviorException_IsTranslated()
        {
            var disk = new IOException("inner");
            var stunt = Stunt.For<Store>();
            stunt.AddBehavior(new ExceptionMappingBehavior(Wrap));
            stunt.AddBehavior((invocation, next) =>
            {
                if (invocation.MethodBase is ConstructorInfo)
                    return next(invocation, next);

                throw disk;
            });
            var store = stunt.ToObject();

            var thrown = Assert.Throws<InvalidOperationException>(() => store.Read());

            Assert.Same(disk, thrown.InnerException);
        }

        public void OuterBehaviorException_IsLeftAlone()
        {
            var calls = 0;
            var stunt = Stunt.For<Store>();
            stunt.AddBehavior(
                (invocation, next) => throw new InvalidOperationException("outer"),
                invocation => invocation.MethodBase.Name == nameof(Store.Read));
            stunt.AddBehavior(new ExceptionMappingBehavior(exception =>
            {
                calls++;
                return Wrap(exception);
            }));
            var store = stunt.ToObject();

            var thrown = Assert.Throws<InvalidOperationException>(() => store.Read());

            Assert.Equal("outer", thrown.Message);
            Assert.Null(thrown.InnerException);
            Assert.Equal(0, calls);
        }

        public void SuccessfulCall_DoesNotCallTheMap()
        {
            var calls = 0;
            var store = Create(new ExceptionMappingBehavior(exception =>
            {
                calls++;
                return Wrap(exception);
            }));

            Assert.Equal("ok", store.Read());
            Assert.Equal(7, store.Length());
            Assert.Equal(1, store.CountAsync().GetAwaiter().GetResult());
            Assert.Equal(0, calls);
        }

        public void SameInstance_IsRethrown()
        {
            var disk = new IOException("disk");
            var store = Create(new ExceptionMappingBehavior(static exception => exception));
            store.Failure = disk;

            Assert.Same(disk, Assert.Throws<IOException>(() => store.Read()));
        }

        public void NullMapThrows()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ExceptionMappingBehavior(null!));

            Assert.Equal("map", exception.ParamName);
        }

        static Exception Wrap(Exception exception)
            => exception is IOException ? new InvalidOperationException("mapped", exception) : exception;

        static Store Create(params IStuntBehavior[] behaviors)
        {
            var stunt = Stunt.For<Store>();
            foreach (var behavior in behaviors)
                stunt.AddBehavior(behavior);

            return stunt.ToObject();
        }

        public class Store
        {
            public Exception? Failure;

            public virtual string Read()
            {
                if (Failure != null)
                    throw Failure;

                return "ok";
            }

            public virtual int Length()
            {
                if (Failure != null)
                    throw Failure;

                return 7;
            }

            public virtual void Run()
            {
                if (Failure != null)
                    throw Failure;
            }

            public virtual int[] Items()
            {
                if (Failure != null)
                    throw Failure;

                return new[] { 1 };
            }

            public virtual Task<int> CountAsync()
                => Failure == null ? Task.FromResult(1) : Task.FromException<int>(Failure);

            public virtual ValueTask<int> MeasureAsync()
                => Failure == null ? new ValueTask<int>(3) : ValueTask.FromException<int>(Failure);

            public virtual Task RunAsync()
                => Failure == null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
