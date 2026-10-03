#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Stunts;
using Xunit;

[assembly: Stunt<Stunts.Scenarios.GenericWrappers.IRegistered<int>>]
[assembly: Stunt<Stunts.Scenarios.GenericWrappers.IRegistered<string>, IDisposable>]

namespace Stunts.Scenarios.GenericWrappers
{
    public interface IBox<T>
    {
        T Value { get; }
        U Echo<U>(U value);
        bool TryGet(out T value);
    }

    public interface IEcho<T>
    {
        T Echo(T value) => value;
    }

    public interface ITag<T>
    {
        T Tag { get; }
    }

    public interface IRepeated<T>
    {
    }

    public interface IConstrainedCollection<T>
    {
    }

    public interface IValue<T>
    {
    }

    public interface IRegistered<T>
    {
        T Value { get; }
    }

    public class DualValue : IValue<int>, IValue<string>
    {
    }

    public interface IArrayBox<T>
    {
    }

    public class Box<T, U> where T : class, new() where U : unmanaged
    {
        public Box(T value, U number) => (Value, Number) = (value, number);
        public virtual T Value { get; }
        public virtual U Number { get; }
    }

    public delegate T Echo<T>(T value);

    public record RecordBox<T>(T Value)
    {
        public virtual T Echo(T value) => value;
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            WrappersAreClosedAtCallSites();
            PartiallyClosedAndNestedArguments();
            RepeatedArguments();
            ClassConstraintsAndConstructorArguments();
            AdditionalInterfacesAndBuilder();
            GenericDelegate();
            GenericDefaultImplementation();
            WrapperAndDirectCallShareTheStunt();
            ConstrainedWrappers();
            ConstraintOnlyParameters();
            GenericRecord();
            AmbiguousConstraintInference();
            ArrayDefinitions();
            ChainedWrappers();
            LocalFunctionWrappers();
            AssemblyRegistrations();
        }

        [StuntGenerator]
        static IDictionary<K, V> Create<K, V>() => Stunt.Of<IDictionary<K, V>>();
        [StuntGenerator]
        static IDictionary<Key, Value> Renamed<Key, Value>() => Stunt.Of<IDictionary<Key, Value>>();

        void WrappersAreClosedAtCallSites()
        {
            IDictionary<string, int> first = Create<string, int>();
            IDictionary<Guid, string> second = Renamed<Guid, string>();

            Assert.False(first.GetType().IsGenericType);
            Assert.False(second.GetType().IsGenericType);
            Assert.Same(typeof(Test).Assembly, first.GetType().Assembly);

            var error = Assert.Throws<NotSupportedException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(IDictionary<DateTime, decimal>), Array.Empty<Type>(), Array.Empty<object>()));
            Assert.Contains("[assembly: Stunt<System.Collections.Generic.IDictionary<System.DateTime, decimal>>]", error.Message);
        }

        [StuntGenerator]
        static IBox<Tuple<string, T[]>> Nested<T>() => Stunt.Of<IBox<Tuple<string, T[]>>>();

        void PartiallyClosedAndNestedArguments()
        {
            IBox<Tuple<string, int[]>> box = Nested<int>();
            var recorder = new RecordingBehavior();
            Stunt.Get(box).AddBehavior(recorder).AddBehavior(new DefaultValueBehavior());

            Assert.Null(box.Value);
            Assert.Equal(0, box.Echo(42));
            Assert.False(box.TryGet(out var value));
            Assert.Null(value);

            var getter = Assert.IsAssignableFrom<MethodInfo>(recorder.Invocations[0].Invocation.MethodBase);
            Assert.Equal(typeof(Tuple<string, int[]>), getter.ReturnType);
            var method = Assert.IsAssignableFrom<MethodInfo>(recorder.Invocations[1].Invocation.MethodBase);
            Assert.Equal(typeof(int), method.GetGenericArguments()[0]);
        }

        [StuntGenerator]
        static IRepeated<Tuple<T, T>> Repeated<T>() => Stunt.Of<IRepeated<Tuple<T, T>>>();

        void RepeatedArguments()
        {
            Assert.IsAssignableFrom<IStunt>(Repeated<int>());
            Assert.Throws<NotSupportedException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(IRepeated<Tuple<int, string>>), Array.Empty<Type>(), Array.Empty<object>()));
        }

        [StuntGenerator]
        static Box<T, U> Constrained<T, U>(T value, U number) where T : class, new() where U : unmanaged
            => Stunt.Of<Box<T, U>>(value, number);

        void ClassConstraintsAndConstructorArguments()
        {
            var value = new StringBuilder("base");
            Box<StringBuilder, int> box = Constrained(value, 42);

            Assert.Same(value, box.Value);
            Assert.Equal(42, box.Number);

            Assert.Throws<MissingMethodException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(Box<StringBuilder, int>), Array.Empty<Type>(), new object[] { "wrong", 42 }));
        }

        [StuntGenerator]
        static StuntReference<IBox<T>> Reference<T>()
            => Stunt.For<IBox<T>, ITag<List<T>>, IDisposable>();

        [StuntGenerator]
        static IBox<T> Build<T>(StuntBuilder builder)
            => builder.Build<IBox<T>, ITag<List<T>>, IDisposable>();

        [StuntGenerator]
        static IBox<T> Plain<T>() => Stunt.Of<IBox<T>>();

        void AdditionalInterfacesAndBuilder()
        {
            IBox<int> first = Reference<int>().AddBehavior(new DefaultValueBehavior()).ToObject();
            IBox<string> second = Build<string>(Stunt.Builder().AddBehavior(new DefaultValueBehavior()));

            Assert.Equal(0, first.Value);
            Assert.Null(second.Value);
            Assert.Null(((ITag<List<int>>)first).Tag);
            Assert.IsAssignableFrom<IDisposable>(second);

            var reordered = new CompiledStuntFactory().CreateStunt(typeof(Test).Assembly, typeof(IBox<int>),
                new[] { typeof(IDisposable), typeof(ITag<List<int>>) }, Array.Empty<object>());
            Assert.Equal(first.GetType(), reordered.GetType());
        }

        [StuntGenerator]
        static Echo<T> Delegate<T>(Echo<T> implementation) => Stunt.Of<Echo<T>>(implementation);

        void GenericDelegate()
        {
            Echo<int> echo = Delegate<int>(value => value + 1);

            Assert.Equal(43, echo(42));
            Assert.IsAssignableFrom<IStunt>(echo.Target);
            Stunt.Get(echo).AddBehavior((invocation, next) => invocation.CreateValueReturn(100));
            Assert.Equal(100, echo(42));
        }

        [StuntGenerator]
        static IEcho<T> Default<T>() => Stunt.Of<IEcho<T>>();

        void GenericDefaultImplementation()
        {
            IEcho<string> echo = Default<string>();

            Assert.Equal("Ada", echo.Echo("Ada"));
            Stunt.Get(echo).AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(next(invocation, next).ReturnValue + "!"));
            Assert.Equal("Ada!", echo.Echo("Ada"));
        }

        void WrapperAndDirectCallShareTheStunt()
        {
            IBox<int> direct = Stunt.Of<IBox<int>>();
            IBox<int> wrapped = Plain<int>();

            Assert.Equal(direct.GetType(), wrapped.GetType());
        }

        [StuntGenerator]
        static ITag<T> Struct<T>() where T : struct => Stunt.Of<ITag<T>>();
        [StuntGenerator]
        static ITag<T> Class<T>() where T : class => Stunt.Of<ITag<T>>();

        void ConstrainedWrappers()
        {
            ITag<int> first = Struct<int>();
            ITag<string> second = Class<string>();

            Assert.IsAssignableFrom<IStunt>(first);
            Assert.IsAssignableFrom<IStunt>(second);
        }

        [StuntGenerator]
        static IConstrainedCollection<T> WithConstraint<T, U>() where T : class, IEnumerable<U> where U : struct
            => Stunt.Of<IConstrainedCollection<T>>();

        void ConstraintOnlyParameters()
        {
            IConstrainedCollection<List<int>> box = WithConstraint<List<int>, int>();

            Assert.IsAssignableFrom<IStunt>(box);
        }

        [StuntGenerator]
        static RecordBox<T> Record<T>(T value) => Stunt.Of<RecordBox<T>>(value);

        void GenericRecord()
        {
            RecordBox<string> box = Record("Ada");

            Assert.Equal("Ada", box.Value);
            Assert.Equal("Bea", box.Echo("Bea"));
        }

        [StuntGenerator]
        static IConstrainedCollection<T> Ambiguous<T, U>() where T : IValue<U> where U : class
            => Stunt.Of<IConstrainedCollection<T>>();

        void AmbiguousConstraintInference()
        {
            IConstrainedCollection<DualValue> box = Ambiguous<DualValue, string>();

            Assert.IsAssignableFrom<IStunt>(box);
        }

        [StuntGenerator]
        static IArrayBox<T[]> Vector<T>() => Stunt.Of<IArrayBox<T[]>>();

        void ArrayDefinitions()
        {
            Assert.IsAssignableFrom<IStunt>(Vector<int>());
            var nonVector = typeof(IArrayBox<>).MakeGenericType(typeof(int).MakeArrayType(1));

            Assert.Throws<NotSupportedException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, nonVector, Array.Empty<Type>(), Array.Empty<object>()));
        }

        [StuntGenerator]
        static IList<ITag<T>> Outer<T>() => Middle<ITag<T>>();
        [StuntGenerator]
        static IList<T> Middle<T>() => Inner<IList<T>, T>();
        [StuntGenerator]
        static TList Inner<TList, TItem>() where TList : IList<TItem> => Stunt.Of<TList>();

        void ChainedWrappers()
        {
            IList<ITag<Guid>> list = Outer<Guid>();

            Assert.IsAssignableFrom<IStunt>(list);
            Assert.False(list.GetType().IsGenericType);
        }

        void LocalFunctionWrappers()
        {
            ITag<DateTime> tag = Local<DateTime>();

            Assert.IsAssignableFrom<IStunt>(tag);

            [StuntGenerator]
            static ITag<T> Local<T>() => Stunt.Of<ITag<T>>();
        }

        void AssemblyRegistrations()
        {
            var factory = new CompiledStuntFactory();
            var first = factory.CreateStunt(typeof(Test).Assembly, typeof(IRegistered<int>), Array.Empty<Type>(), Array.Empty<object>());
            var second = factory.CreateStunt(typeof(Test).Assembly, typeof(IRegistered<string>), new[] { typeof(IDisposable) }, Array.Empty<object>());

            Assert.IsAssignableFrom<IRegistered<int>>(first);
            Assert.IsAssignableFrom<IDisposable>(second);
            Assert.IsAssignableFrom<IStunt>(second);
        }
    }
}
