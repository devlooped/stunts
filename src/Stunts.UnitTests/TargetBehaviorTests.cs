using System;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class TargetBehaviorTests
    {
        static readonly MethodInfo Add = typeof(Calculator).GetMethod(nameof(Calculator.Add))!;
        static readonly ConstructorInfo Constructor = typeof(Calculator).GetConstructor(Type.EmptyTypes)!;

        static readonly MethodInvoker Invoker = (target, call) =>
        {
            var calculator = (Calculator)target;
            return call.CreateValueReturn(calculator.Add(
                call.Arguments.Get<int>("x"),
                call.Arguments.Get<int>("y")));
        };

        [Fact]
        public void NullArgumentsThrow()
        {
            var target = Assert.Throws<ArgumentNullException>(() => new TargetBehavior((object)null!));
            var factory = Assert.Throws<ArgumentNullException>(() => new TargetBehavior((Func<object?>)null!));

            Assert.Equal("target", target.ParamName);
            Assert.Equal("factory", factory.ParamName);
        }

        [Fact]
        public void SkipsConstructorsAndIdentityMembers()
        {
            var behavior = new TargetBehavior(new Calculator());
            var stunt = new object();

            Assert.False(behavior.AppliesTo(MethodInvocation.Create(stunt, Constructor)));
            Assert.False(behavior.AppliesTo(MethodInvocation.Create(stunt, typeof(object).GetMethod(nameof(object.Equals), new[] { typeof(object) })!, stunt)));
            Assert.False(behavior.AppliesTo(MethodInvocation.Create(stunt, typeof(object).GetMethod(nameof(object.GetHashCode))!)));
            Assert.False(behavior.AppliesTo(MethodInvocation.Create(stunt, typeof(object).GetMethod(nameof(object.ToString))!)));
            Assert.True(behavior.AppliesTo(MethodInvocation.Create(stunt, Add, Invoker, 1, 2)));
        }

        [Fact]
        public void ForwardsToTheInstance()
        {
            var calculator = new Calculator();
            var behavior = new TargetBehavior(calculator);
            var invocation = MethodInvocation.Create(new object(), Add, Invoker, 2, 3);

            var result = behavior.Execute(invocation, NextFails);

            Assert.Equal(5, result.ReturnValue);
            Assert.Equal(1, calculator.Adds);
        }

        [Fact]
        public void TargetExceptionKeepsItsIdentity()
        {
            var calculator = new Calculator { Error = new InvalidOperationException("boom") };
            var behavior = new TargetBehavior(calculator);
            var invocation = MethodInvocation.Create(new object(), Add, Invoker, 2, 3);

            var thrown = Assert.Throws<InvalidOperationException>(() => behavior.Execute(invocation, NextFails));

            Assert.Same(calculator.Error, thrown);
        }

        [Fact]
        public void NullFactoryResultCallsNext()
        {
            var runs = 0;
            var behavior = new TargetBehavior(() =>
            {
                runs++;
                return null;
            });
            var invocation = MethodInvocation.Create(new object(), Add, Invoker, 2, 3);
            var calls = 0;

            behavior.Execute(invocation, (call, next) =>
            {
                calls++;
                Assert.Same(invocation, call);
                return call.CreateReturn();
            });
            behavior.Execute(invocation, (call, next) =>
            {
                calls++;
                return call.CreateReturn();
            });

            Assert.Equal(1, runs);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void FactoryRunsOncePerBehavior()
        {
            var runs = 0;
            Calculator? created = null;
            TargetBehavior Create() => new TargetBehavior(() =>
            {
                runs++;
                created = new Calculator();
                return created;
            });

            var first = Create();
            var invocation = MethodInvocation.Create(new object(), Add, Invoker, 2, 3);
            first.Execute(invocation, NextFails);
            first.Execute(invocation, NextFails);

            Assert.Equal(1, runs);
            Assert.Equal(2, created!.Adds);

            Create().Execute(invocation, NextFails);

            Assert.Equal(2, runs);
        }

        static IMethodReturn NextFails(IMethodInvocation invocation, ExecuteHandler next)
            => throw new InvalidOperationException("next");

        public class Calculator
        {
            public Exception? Error { get; set; }

            public int Adds { get; private set; }

            public int Add(int x, int y)
            {
                Adds++;
                if (Error != null)
                    throw Error;

                return x + y;
            }
        }
    }
}
