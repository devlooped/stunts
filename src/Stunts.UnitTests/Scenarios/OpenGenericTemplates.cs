#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Stunts.Scenarios.OpenGenericTemplates
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

    public partial class Outer<T0>
    {
        public interface IInner<U>
        {
            T0 Get(U value);
        }

        class Hidden
        {
            public virtual T0 Echo(T0 value) => value;
        }

        public static IInner<U> Inner<U>() => Stunt.Of<IInner<U>>();
        public static object HiddenInstance() => Stunt.Of<Hidden>();
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            CanonicalTemplatesAreReused();
            PartiallyClosedAndNestedArguments();
            RepeatedArgumentsMustMatch();
            ClassConstraintsAndConstructorArguments();
            AdditionalInterfacesAndBuilder();
            GenericDelegate();
            GenericDefaultImplementation();
            ClosedProxyTakesPrecedence();
            DifferentConstraintsDoNotCollide();
            NestedGenericTypes();
            ConstraintOnlyParameters();
            GenericRecord();
            AmbiguousConstraintInference();
            ArrayShapesMustMatch();
        }

        static IDictionary<K, V> Create<K, V>() => Stunt.Of<IDictionary<K, V>>();
        static IDictionary<Key, Value> Renamed<Key, Value>() => Stunt.Of<IDictionary<Key, Value>>();

        void CanonicalTemplatesAreReused()
        {
            IDictionary<string, int> first = Create<string, int>();
            IDictionary<Guid, string> second = Renamed<Guid, string>();

            Assert.Equal(first.GetType().GetGenericTypeDefinition(), second.GetType().GetGenericTypeDefinition());
            Assert.Same(typeof(Test).Assembly, first.GetType().Assembly);
            Assert.Single(typeof(Test).Assembly.GetTypes(), type => type.IsGenericTypeDefinition &&
                type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>)));
        }

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
            Assert.False(getter.DeclaringType!.ContainsGenericParameters);
            var method = Assert.IsAssignableFrom<MethodInfo>(recorder.Invocations[1].Invocation.MethodBase);
            Assert.Equal(typeof(int), method.GetGenericArguments()[0]);
            Assert.Equal(typeof(int), method.GetParameters()[0].ParameterType);
        }

        static IRepeated<Tuple<T, T>> Repeated<T>() => Stunt.Of<IRepeated<Tuple<T, T>>>();

        void RepeatedArgumentsMustMatch()
        {
            Assert.IsAssignableFrom<IStunt>(Repeated<int>());
            Assert.Throws<ArgumentException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(IRepeated<Tuple<int, string>>), Array.Empty<Type>(), Array.Empty<object>()));
        }

        static Box<T, U> Constrained<T, U>(T value, U number) where T : class, new() where U : unmanaged
            => Stunt.Of<Box<T, U>>(value, number);

        void ClassConstraintsAndConstructorArguments()
        {
            var value = new StringBuilder("base");
            Box<StringBuilder, int> box = Constrained(value, 42);

            Assert.Same(value, box.Value);
            Assert.Equal(42, box.Number);
            var parameters = box.GetType().GetGenericTypeDefinition().GetGenericArguments();
            Assert.True(parameters[0].GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
            Assert.True(parameters[0].GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
            Assert.True(parameters[1].GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint));
        }

        static StuntReference<IBox<T>> Reference<T>()
            => Stunt.For<IBox<T>, ITag<List<T>>, IDisposable>();

        static IBox<T> Build<T>(StuntBuilder builder)
            => builder.Build<IBox<T>, ITag<List<T>>, IDisposable>();

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

        static Echo<T> Delegate<T>(Echo<T> implementation) => Stunt.Of<Echo<T>>(implementation);

        void GenericDelegate()
        {
            Echo<int> echo = Delegate<int>(value => value + 1);

            Assert.Equal(43, echo(42));
            Assert.IsAssignableFrom<IStunt>(echo.Target);
            Stunt.Get(echo).AddBehavior((invocation, next) => invocation.CreateValueReturn(100));
            Assert.Equal(100, echo(42));
        }

        static IEcho<T> Default<T>() => Stunt.Of<IEcho<T>>();

        void GenericDefaultImplementation()
        {
            IEcho<string> echo = Default<string>();

            Assert.Equal("Ada", echo.Echo("Ada"));
            Stunt.Get(echo).AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(next(invocation, next).ReturnValue + "!"));
            Assert.Equal("Ada!", echo.Echo("Ada"));
        }

        void ClosedProxyTakesPrecedence()
        {
            IBox<int> direct = Stunt.Of<IBox<int>>();
            IBox<int> throughTemplate = Plain<int>();
            var fromFactory = new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(IBox<int>), Array.Empty<Type>(), Array.Empty<object>());

            Assert.False(direct.GetType().IsGenericType);
            Assert.Equal(direct.GetType(), fromFactory.GetType());
            Assert.Equal(direct.GetType(), throughTemplate.GetType());
        }

        static ITag<T> Struct<T>() where T : struct => Stunt.Of<ITag<T>>();
        static ITag<T> Class<T>() where T : class => Stunt.Of<ITag<T>>();

        void DifferentConstraintsDoNotCollide()
        {
            ITag<int> first = Struct<int>();
            ITag<string> second = Class<string>();

            Assert.NotEqual(first.GetType().GetGenericTypeDefinition(), second.GetType().GetGenericTypeDefinition());
            Assert.IsAssignableFrom<IStunt>(first);
            Assert.IsAssignableFrom<IStunt>(second);
        }

        void NestedGenericTypes()
        {
            Outer<string>.IInner<int> inner = Outer<string>.Inner<int>();
            Stunt.Get(inner).AddBehavior(new DefaultValueBehavior());

            Assert.Null(inner.Get(42));

            var hidden = Outer<string>.HiddenInstance();
            Assert.Equal("Ada", hidden.GetType().GetMethod("Echo")!.Invoke(hidden, new object[] { "Ada" }));
        }

        static IConstrainedCollection<T> WithConstraint<T, U>() where T : class, IEnumerable<U> where U : struct
            => Stunt.Of<IConstrainedCollection<T>>();

        void ConstraintOnlyParameters()
        {
            IConstrainedCollection<List<int>> box = WithConstraint<List<int>, int>();

            Assert.IsAssignableFrom<IStunt>(box);
            Assert.Equal(new[] { typeof(List<int>), typeof(int) }, box.GetType().GetGenericArguments());

            var error = Assert.Throws<ArgumentException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, typeof(IConstrainedCollection<List<string>>), Array.Empty<Type>(), Array.Empty<object>()));
            Assert.IsType<ArgumentException>(error.InnerException);
        }

        static RecordBox<T> Record<T>(T value) => Stunt.Of<RecordBox<T>>(value);

        void GenericRecord()
        {
            RecordBox<string> box = Record("Ada");

            Assert.Equal("Ada", box.Value);
            Assert.Equal("Bea", box.Echo("Bea"));
        }

        static IConstrainedCollection<T> Ambiguous<T, U>() where T : IValue<U> where U : class
            => Stunt.Of<IConstrainedCollection<T>>();

        void AmbiguousConstraintInference()
        {
            IConstrainedCollection<DualValue> box = Ambiguous<DualValue, string>();

            Assert.Equal(new[] { typeof(DualValue), typeof(string) }, box.GetType().GetGenericArguments());
        }

        static IArrayBox<T[]> Vector<T>() => Stunt.Of<IArrayBox<T[]>>();

        void ArrayShapesMustMatch()
        {
            Assert.IsAssignableFrom<IStunt>(Vector<int>());
            var nonVector = typeof(IArrayBox<>).MakeGenericType(typeof(int).MakeArrayType(1));

            Assert.Throws<ArgumentException>(() => new CompiledStuntFactory().CreateStunt(
                typeof(Test).Assembly, nonVector, Array.Empty<Type>(), Array.Empty<object>()));
        }
    }
}
