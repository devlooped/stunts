using System;
using System.Reflection;
using System.Text;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Generic classes, interfaces, methods, and constraints from the Castle suites.
    /// </summary>
    public class GenericsTests : IRunnable
    {
        public void Run()
        {
            ClosedGenericInterface();
            ClosedGenericClassUsesTheSuppliedConstructor();
            ClassInheritingAnOpenGenericCanBeProxiedClosed();
            GenericConstraintsAreGenerated();
            GenericInterfaceMethodSeesTheConstructedMethod();
            GenericClassMethodProceedsToBase();
            GenericMethodWithInAndOutParameters();
        }

        public void GenericInterfaceMethodSeesTheConstructedMethod()
        {
            IBox<string> stunt = Stunt.For<IBox<string>>().AddBehavior((invocation, next) =>
            {
                var method = (MethodInfo)invocation.MethodBase;

                Assert.False(method.IsGenericMethodDefinition);
                Assert.Equal(typeof(int), method.GetGenericArguments()[0]);
                Assert.Equal(typeof(int), method.GetParameters()[0].ParameterType);

                return invocation.CreateValueReturn(invocation.Arguments.Get<int>("value") * 2);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(42, stunt.Echo(21));
        }

        public void GenericClassMethodProceedsToBase()
        {
            var stunt = Stunt.For<Box<string>>("Ada");
            Box<string> box = stunt.ToObject();

            Assert.Equal("Bea", box.Echo("Bea"));
            Assert.Null(box.Echo<string>(null!));

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Box<string>.Echo)
                    ? invocation.CreateValueReturn("proxy")
                    : next(invocation, next));

            Assert.Equal("proxy", box.Echo("Bea"));
        }

        public void GenericMethodWithInAndOutParameters()
        {
            IGenericParameters stunt = Stunt.For<IGenericParameters>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(IGenericParameters.Read)
                    ? invocation.CreateValueReturn(invocation.Arguments.Get<Point>("value").X)
                    : invocation.CreateValueReturn(true, "Ada"),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var point = new Point(6);

            Assert.Equal(6, stunt.Read(in point));
            Assert.True(stunt.TryGet<string>(out var value));
            Assert.Equal("Ada", value);
        }

        public void ClosedGenericInterface()
        {
            IBox<string> stunt = Stunt.For<IBox<string>>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name.StartsWith("set_", StringComparison.Ordinal)
                    ? invocation.CreateReturn()
                    : invocation.CreateValueReturn("Ada")).ToObject();

            Assert.Equal("Ada", stunt.Value);
        }

        public void ClosedGenericClassUsesTheSuppliedConstructor()
        {
            Box<string> stunt = Stunt.Of<Box<string>>("Ada");

            Assert.Equal("Ada", stunt.Value);
            Assert.Equal("Ada!", stunt.Decorate("!"));
        }

        public void ClassInheritingAnOpenGenericCanBeProxiedClosed()
        {
            StringBox stunt = Stunt.Of<StringBox>();

            Assert.Equal("base", stunt.Value);
        }

        public void GenericConstraintsAreGenerated()
        {
            IConstraints stunt = Stunt.For<IConstraints>().AddBehavior(new DefaultValueBehavior()).ToObject();

            stunt.ClassAndNew<StringBuilder>();
            stunt.Struct<int>();
            stunt.Unmanaged<int>();
            stunt.Enum<StringComparison>();
            stunt.NotNull<string>();
            stunt.Base<StringBuilder, object>();
        }

        public interface IBox<T>
        {
            T Value { get; set; }

            U Echo<U>(U value);
        }

        public class Box<T>
        {
            public Box(T value) => Value = value;

            public virtual T Value { get; }

            public virtual U Echo<U>(U value) where U : class => value;

            public virtual string Decorate(string suffix) => Value + suffix;
        }

        public class StringBox : Box<string>
        {
            public StringBox() : base("base") { }
        }

        public readonly struct Point
        {
            public Point(int x) => X = x;

            public int X { get; }
        }

        public interface IGenericParameters
        {
            int Read<T>(in T value);

            bool TryGet<T>(out T value);
        }

        public interface IConstraints
        {
            void ClassAndNew<T>() where T : class, new();

            void Struct<T>() where T : struct;

            void Unmanaged<T>() where T : unmanaged;

            void Enum<T>() where T : Enum;

            void NotNull<T>() where T : notnull;

            void Base<T, TBase>() where T : class, TBase;
        }
    }
}
