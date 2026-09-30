using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Castle composition proxies (interface with target, swappable target,
    /// class proxy with target, mixins) expressed as behaviors.
    /// </summary>
    public class CompositionTests : IRunnable
    {
        public void Run()
        {
            InterfaceProxyForwardsToATarget();
            TargetCanBeSwappedForTheSameProxy();
            ClassProxyForwardsVirtualCallsAndKeepsItsOwnNonVirtualState();
            MixinForwardsEachInterfaceToItsOwnInstance();
        }

        public void InterfaceProxyForwardsToATarget()
        {
            var target = new Adder();
            ICalculator stunt = Stunt.For<ICalculator>().AddBehavior(
                Forward(target), invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(5, stunt.Add(2, 3));
            Assert.NotSame(target, stunt);
        }

        public void TargetCanBeSwappedForTheSameProxy()
        {
            ICalculator current = new Adder();
            ICalculator stunt = Stunt.For<ICalculator>().AddBehavior(
                (invocation, next) => Forward(current)(invocation, next),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(5, stunt.Add(2, 3));

            current = new Multiplier();

            Assert.Equal(6, stunt.Add(2, 3));
        }

        public void ClassProxyForwardsVirtualCallsAndKeepsItsOwnNonVirtualState()
        {
            var target = new Store { Stored = 7 };
            Store stunt = Stunt.For<Store>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(Store.Read)
                    ? invocation.CreateValueReturn(target.Read())
                    : next(invocation, next)).ToObject();

            Assert.Equal(7, stunt.Read());
            Assert.Equal(0, stunt.ReadFixed());
        }

        public void MixinForwardsEachInterfaceToItsOwnInstance()
        {
            var name = new NameMixin();
            var age = new AgeMixin();
            IName stunt = Stunt.For<IName, IAge>().AddBehavior((invocation, next) =>
            {
                var target = invocation.MethodBase.Name == "get_" + nameof(IName.Name) ? (object)name : age;
                return invocation.CreateValueReturn(Invoke(target, invocation));
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal("Ada", stunt.Name);
            Assert.Equal(36, ((IAge)stunt).Age);
        }

        static ExecuteHandler Forward(object target)
            => (invocation, next) => invocation.CreateValueReturn(Invoke(target, invocation));

        static object Invoke(object target, IMethodInvocation invocation)
        {
            var parameters = invocation.MethodBase.GetParameters();
            var args = parameters.Select((parameter, index) => invocation.Arguments.GetValue(index)).ToArray();
            var method = target.GetType().GetMethod(
                invocation.MethodBase.Name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameters.Select(parameter => parameter.ParameterType).ToArray(),
                null);
            return method.Invoke(target, args);
        }

        public interface ICalculator
        {
            int Add(int x, int y);
        }

        public class Adder : ICalculator
        {
            public int Add(int x, int y) => x + y;
        }

        public class Multiplier : ICalculator
        {
            public int Add(int x, int y) => x * y;
        }

        public class Store
        {
            public int Stored { get; set; }

            public virtual int Read() => Stored;

            public int ReadFixed() => Stored;
        }

        public interface IName
        {
            string Name { get; }
        }

        public interface IAge
        {
            int Age { get; }
        }

        public class NameMixin : IName
        {
            public string Name => "Ada";
        }

        public class AgeMixin : IAge
        {
            public int Age => 36;
        }
    }
}
