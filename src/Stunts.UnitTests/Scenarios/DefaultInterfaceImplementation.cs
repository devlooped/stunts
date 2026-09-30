using System;
using System.Collections.Generic;
using System.Text;
using Sample;
using Xunit;

namespace Stunts.Scenarios.DefaultInterfaceImplementation
{
    public interface IDefault
    {
        void Do();
        int Value => 5;
        string Greet(string name) => "Hello " + name;
        string this[int index] => "#" + index;
        event EventHandler Changed { add { } remove { } }
    }

    public interface IDerived : IDefault
    {
        int IDefault.Value => 6;
    }

    public interface IEcho<T>
    {
        T Echo(T value) => value;
    }

    public class Impl : IDefault
    {
        public virtual void Do() { }
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            DefaultsRunWhenNotIntercepted();
            DefaultsCanBeReplaced();
            DefaultsCanBeProceededTo();
            DefaultEventIsIntercepted();
            DerivedInterfaceDefaultIsMostSpecific();
            GenericInterfaceDefault();
            ClassWithAdditionalInterfaceDefault();
            DefaultImplementationClass();
        }

        public void DefaultsRunWhenNotIntercepted()
        {
            IDefault stunt = Stunt.Of<IDefault>();

            Assert.Equal(5, stunt.Value);
            Assert.Equal("Hello Ada", stunt.Greet("Ada"));
            Assert.Equal("#3", stunt[3]);
            stunt.Changed += (sender, args) => { };
            Assert.Throws<NotImplementedException>(() => stunt.Do());
        }

        public void DefaultsCanBeReplaced()
        {
            IDefault stunt = Stunt.For<IDefault>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "get_Value"
                    ? invocation.CreateValueReturn(42)
                    : next(invocation, next)).ToObject();

            Assert.Equal(42, stunt.Value);
            Assert.Equal("Hello Ada", stunt.Greet("Ada"));
        }

        public void DefaultsCanBeProceededTo()
        {
            IDefault stunt = Stunt.For<IDefault>().AddBehavior((invocation, next) =>
            {
                var result = next(invocation, next);
                return invocation.MethodBase.Name == nameof(IDefault.Greet)
                    ? invocation.CreateValueReturn(result.ReturnValue + "!")
                    : result;
            }).ToObject();

            Assert.Equal("Hello Ada!", stunt.Greet("Ada"));
            Assert.Equal(5, stunt.Value);
        }

        public void DefaultEventIsIntercepted()
        {
            var added = 0;
            IDefault stunt = Stunt.For<IDefault>().AddBehavior((invocation, next) =>
            {
                if (invocation.MethodBase.Name == "add_Changed")
                    added++;
                return next(invocation, next);
            }).ToObject();

            stunt.Changed += (sender, args) => { };

            Assert.Equal(1, added);
        }

        public void DerivedInterfaceDefaultIsMostSpecific()
        {
            IDerived stunt = Stunt.Of<IDerived>();

            Assert.Equal(6, stunt.Value);
            Assert.Equal("Hello Ada", stunt.Greet("Ada"));
        }

        public void GenericInterfaceDefault()
        {
            var stunt = Stunt.For<IEcho<string>>();
            IEcho<string> echo = stunt.ToObject();

            Assert.Equal("Ada", echo.Echo("Ada"));

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn("Bea"));

            Assert.Equal("Bea", echo.Echo("Ada"));
        }

        public void ClassWithAdditionalInterfaceDefault()
        {
            var stunt = Stunt.For<Impl, IDefault>();
            Impl impl = stunt.ToObject();

            Assert.Equal(5, ((IDefault)impl).Value);

            stunt.AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == "get_Value"
                    ? invocation.CreateValueReturn(42)
                    : next(invocation, next));

            Assert.Equal(42, ((IDefault)impl).Value);
        }

        public void DefaultImplementationClass()
        {
            var type = typeof(Test).Assembly.GetType("Stunts.Stunts.Scenarios.DefaultInterfaceImplementation.DefaultIDefault");

            Assert.NotNull(type);
            Assert.True(type!.IsPublic);
            Assert.True(typeof(IDefault).IsAssignableFrom(type));
            // Only members without a default are implemented.
            Assert.NotNull(type.GetMethod(nameof(IDefault.Do)));
            Assert.Null(type.GetProperty(nameof(IDefault.Value)));
            Assert.Null(type.GetMethod(nameof(IDefault.Greet)));

            var instance = (IDefault)type.GetProperty("Default")!.GetValue(null)!;

            Assert.IsType(type, instance);
            Assert.Equal(5, instance.Value);
            Assert.Throws<NotImplementedException>(() => instance.Do());
        }
    }
}
