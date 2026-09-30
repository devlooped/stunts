using System;
using System.Collections.Generic;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// A delegate stunt is a real delegate. Its target is the generated instance,
    /// and an implementation argument is the pipeline tail.
    /// </summary>
    public partial class DelegateTests : IRunnable
    {
        public void Run()
        {
            BehaviorHandlesACallWithNoTarget();
            NoTargetLeavesTheTailEmpty();
            ImplementationIsTheTail();
            BehaviorWrapsTheImplementation();
            BehaviorCanReplaceAnArgument();
            RefAndReturnAreWrittenBack();
            NullImplementationThrows();
            PrivateNestedDelegateForwards();
            ParameterNamedMethodForwards();
            ParametersThatHideFieldsForward();
        }

        public void BehaviorHandlesACallWithNoTarget()
        {
            var seen = false;
            Action action = Stunt.For<Action>().AddBehavior((invocation, next) =>
            {
                seen = invocation.MethodBase.Name == "Invoke";
                return invocation.CreateReturn();
            }).ToObject();

            action();

            Assert.True(seen);
            Assert.IsAssignableFrom<IStunt>(action.Target);
        }

        public void NoTargetLeavesTheTailEmpty()
        {
            bool? hasImplementation = null;
            Action action = Stunt.For<Action>().AddBehavior((invocation, next) =>
            {
                hasImplementation = invocation.HasImplementation;
                return next(invocation, next);
            }).ToObject();

            Assert.Throws<NotImplementedException>(() => action());
            Assert.False(hasImplementation);
        }

        public void ImplementationIsTheTail()
        {
            var called = false;
            Action implementation = () => called = true;
            Action action = Stunt.Of<Action>(implementation);

            action();

            Assert.True(called);
            Assert.NotSame(implementation, action);
        }

        public void BehaviorWrapsTheImplementation()
        {
            var order = new List<string>();
            Action implementation = () => order.Add("impl");
            Action action = Stunt.For<Action>(implementation).AddBehavior((invocation, next) =>
            {
                order.Add("before");
                var result = next(invocation, next);
                order.Add("after");
                return result;
            }).ToObject();

            action();

            Assert.Equal(new[] { "before", "impl", "after" }, order);
        }

        public void BehaviorCanReplaceAnArgument()
        {
            var seen = 0;
            Action<int> implementation = value => seen = value;
            Action<int> action = Stunt.For<Action<int>>(implementation).AddBehavior((invocation, next) =>
            {
                invocation.Arguments.SetValue("obj", 41);
                return next(invocation, next);
            }).ToObject();

            action(1);

            Assert.Equal(41, seen);
        }

        public void RefAndReturnAreWrittenBack()
        {
            RefImpl implementation = (ref int value) =>
            {
                var result = value + 1;
                value = result;
                return result;
            };
            RefImpl action = Stunt.For<RefImpl>(implementation).AddBehavior((invocation, next) =>
            {
                invocation.Arguments.SetValue("value", 4);
                return next(invocation, next);
            }).ToObject();

            var value = 1;
            var returned = action(ref value);

            Assert.Equal(5, returned);
            Assert.Equal(5, value);
        }

        public void NullImplementationThrows()
            => Assert.Throws<ArgumentNullException>(() => Stunt.Of<Action>((Action)null!));

        public void PrivateNestedDelegateForwards()
        {
            var outer = new Outer();
            outer.Run();
        }

        public void ParameterNamedMethodForwards()
        {
            var seen = 0;
            Named implementation = method => seen = method;
            Named action = Stunt.Of<Named>(implementation);

            action(7);

            Assert.Equal(7, seen);
        }

        public void ParametersThatHideFieldsForward()
        {
            var seen = "";
            HidesFields target = (left, right) => seen = left + ":" + right;
            HidesFields action = Stunt.Of<HidesFields>(target);

            action(2, 9);

            Assert.Equal("2:9", seen);
        }

        public delegate int RefImpl(ref int value);

        public delegate void Named(int method);

        public delegate void HidesFields(int implementation, int pipeline);

        public partial class Outer
        {
            public void Run()
            {
                var called = false;
                Hidden implementation = () => called = true;
                Hidden action = Stunt.Of<Hidden>(implementation);

                action();

                Assert.True(called);
            }

            delegate void Hidden();
        }
    }
}
