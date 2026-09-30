using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Castle interface proxies without a target, plus additional interfaces
    /// and a class used as the base type of an interface proxy.
    /// </summary>
    public class InterfaceProxyTests : IRunnable
    {
        public void Run()
        {
            InterfaceWithoutTargetThrowsUntilABehaviorReturns();
            InheritedInterfaceMembersAreImplemented();
            AdditionalInterfacesAreImplemented();
            ClassCanBeTheBaseOfAdditionalInterfaces();
            IdenticalSignaturesShareOneImplementation();
            SameGenericInterfaceTwiceUsesAnExplicitImplementation();
            CovariantInterfaceMembersAreBothCallable();
            EmptyGenericMarkerInterface();
            NonVirtualInterfaceImplementationIsNotReimplemented();
            VirtualInterfaceImplementationIsIntercepted();
        }

        public void InterfaceWithoutTargetThrowsUntilABehaviorReturns()
        {
            var stunt = Stunt.For<ICalculator>();
            ICalculator calculator = stunt.ToObject();

            Assert.IsAssignableFrom<IStunt>(calculator);
            Assert.Throws<NotImplementedException>(() => calculator.Add(1, 2));

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(3));

            Assert.Equal(3, calculator.Add(1, 2));
        }

        public void InheritedInterfaceMembersAreImplemented()
        {
            var seen = new List<string>();
            IDerived stunt = Stunt.For<IDerived>().AddBehavior((invocation, next) =>
            {
                seen.Add(invocation.MethodBase.Name);
                return invocation.CreateReturn();
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            stunt.Base();
            stunt.Derived();

            Assert.Equal(new[] { nameof(IBase.Base), nameof(IDerived.Derived) }, seen);
        }

        public void AdditionalInterfacesAreImplemented()
        {
            ICalculator stunt = Stunt.Of<ICalculator, IDisposable, IServiceProvider>();

            Assert.IsAssignableFrom<IDisposable>(stunt);
            Assert.IsAssignableFrom<IServiceProvider>(stunt);
        }

        public void ClassCanBeTheBaseOfAdditionalInterfaces()
        {
            var stunt = Stunt.For<Calculator, IDisposable>();
            Calculator calculator = stunt.ToObject();

            Assert.IsAssignableFrom<Calculator>(calculator);
            Assert.IsAssignableFrom<IDisposable>(calculator);
            Assert.Equal(3, calculator.Add(1, 2));

            stunt.AddBehavior(new DefaultValueBehavior());
            ((IDisposable)calculator).Dispose();
        }

        public void IdenticalSignaturesShareOneImplementation()
        {
            var calls = 0;
            ILeft stunt = Stunt.For<ILeft, IRight>().AddBehavior((invocation, next) =>
            {
                calls++;
                return invocation.CreateReturn();
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            ((ILeft)stunt).Run();
            ((IRight)stunt).Run();

            Assert.Equal(2, calls);
        }

        public void SameGenericInterfaceTwiceUsesAnExplicitImplementation()
        {
            var values = new Dictionary<string, object>();
            ISlot<int> stunt = Stunt.For<ISlot<int>, ISlot<string>>().AddBehavior((invocation, next) =>
            {
                var method = (MethodInfo)invocation.MethodBase;
                var slot = Slot(method);
                if (invocation.MethodBase.Name.Contains("set_"))
                {
                    values[slot] = invocation.Arguments.GetValue(0);
                    return invocation.CreateReturn();
                }

                values.TryGetValue(slot, out var value);
                return invocation.CreateValueReturn(value);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            ((ISlot<int>)stunt).Value = 4;
            ((ISlot<string>)stunt).Value = "four";

            Assert.Equal(4, ((ISlot<int>)stunt).Value);
            Assert.Equal("four", ((ISlot<string>)stunt).Value);
        }

        static string Slot(MethodBase method)
        {
            var name = method.Name;
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
                return name.Substring(0, dot);

            var info = (MethodInfo)method;
            var type = info.ReturnType == typeof(void)
                ? info.GetParameters()[0].ParameterType
                : info.ReturnType;
            return type.FullName;
        }

        public void CovariantInterfaceMembersAreBothCallable()
        {
            IEnumerable<int> stunt = Stunt.For<IEnumerable<int>>().AddBehavior(new DefaultValueBehavior()).ToObject();

            Assert.Null(stunt.GetEnumerator());
            Assert.Null(((IEnumerable)stunt).GetEnumerator());
        }

        public void EmptyGenericMarkerInterface()
        {
            IMarker<int> stunt = Stunt.Of<IMarker<int>>();

            Assert.IsAssignableFrom<IMarker<int>>(stunt);
        }

        public void NonVirtualInterfaceImplementationIsNotReimplemented()
        {
            var calls = 0;
            SealedImpl stunt = Stunt.For<SealedImpl>().AddBehavior((invocation, next) =>
            {
                calls++;
                return next(invocation, next);
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(5, ((ICalculator)stunt).Add(2, 3));
            Assert.Equal(0, calls);
        }

        public void VirtualInterfaceImplementationIsIntercepted()
        {
            VirtualImpl stunt = Stunt.For<VirtualImpl>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(8)).ToObject();

            Assert.Equal(8, ((ICalculator)stunt).Add(2, 3));
        }

        public interface ICalculator
        {
            int Add(int x, int y);
        }

        public class Calculator : ICalculator
        {
            public virtual int Add(int x, int y) => x + y;
        }

        public interface IBase
        {
            void Base();
        }

        public interface IDerived : IBase
        {
            void Derived();
        }

        public interface ILeft
        {
            void Run();
        }

        public interface IRight
        {
            void Run();
        }

        public interface ISlot<T>
        {
            T Value { get; set; }
        }

        public interface IMarker<T> { }

        public class SealedImpl : ICalculator
        {
            public int Add(int x, int y) => x + y;
        }

        public class VirtualImpl : ICalculator
        {
            public virtual int Add(int x, int y) => x + y;
        }
    }
}
