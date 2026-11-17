using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Stunts;
using Xunit;

namespace Samples.Memoization
{
    public class MemoizeTests
    {
        int calls;

        // The constructor is an invocation too: the test targets only handle the calculator's methods.
        static bool IsMethod(IMethodInvocation invocation) => invocation.MethodBase is MethodInfo;

        // Stands for the target: adds the two arguments and counts each time it runs.
        IMethodReturn Add(IMethodInvocation invocation, ExecuteHandler next)
        {
            Interlocked.Increment(ref calls);
            return invocation.CreateValueReturn(
                (int)invocation.Arguments.GetValue(0)! + (int)invocation.Arguments.GetValue(1)!,
                invocation.Arguments);
        }

        [Fact]
        public void SameArgumentsInvokeTargetOnce()
        {
            var stunt = Stunt.For<ICalculator>().Memoize().AddBehavior(Add, IsMethod);
            ICalculator calculator = stunt.ToObject();

            Assert.Equal(3, calculator.Add(1, 2));
            Assert.Equal(3, calculator.Add(1, 2));

            Assert.Equal(1, calls);
        }

        [Fact]
        public void OtherArgumentsInvokeTargetAgain()
        {
            var stunt = Stunt.For<ICalculator>().Memoize().AddBehavior(Add, IsMethod);
            ICalculator calculator = stunt.ToObject();

            Assert.Equal(3, calculator.Add(1, 2));
            Assert.Equal(5, calculator.Add(2, 3));

            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task ConcurrentCallersShareOneExecution()
        {
            using var running = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            var stunt = Stunt.For<ICalculator>().Memoize().AddBehavior((invocation, next) =>
            {
                running.Set();
                release.Wait();
                return Add(invocation, next);
            }, IsMethod);
            ICalculator calculator = stunt.ToObject();

            var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => calculator.Add(1, 2))).ToArray();

            Assert.True(running.Wait(TimeSpan.FromSeconds(10)));
            // Let the other callers reach the entry the first one is computing.
            await Task.Delay(100);
            release.Set();

            Assert.All(await Task.WhenAll(callers), result => Assert.Equal(3, result));
            Assert.Equal(1, calls);
        }

        [Fact]
        public void FailuresAreNotRemembered()
        {
            var fail = true;
            var stunt = Stunt.For<ICalculator>().Memoize().AddBehavior((invocation, next) =>
            {
                if (fail)
                {
                    fail = false;
                    Interlocked.Increment(ref calls);
                    return invocation.CreateExceptionReturn(new InvalidOperationException());
                }

                return Add(invocation, next);
            }, IsMethod);
            ICalculator calculator = stunt.ToObject();

            Assert.Throws<InvalidOperationException>(() => calculator.Add(1, 2));
            Assert.Equal(3, calculator.Add(1, 2));
            Assert.Equal(3, calculator.Add(1, 2));

            Assert.Equal(2, calls);
        }

        [Fact]
        public void VoidAndRefMethodsAlwaysReachTarget()
        {
            var stunt = Stunt.For<ICalculator>().Memoize().AddBehavior((invocation, next) =>
            {
                Interlocked.Increment(ref calls);
                if (invocation.MethodBase.Name == nameof(ICalculator.TryAdd))
                {
                    var x = (int)invocation.Arguments.GetValue(0)!;
                    var y = (int)invocation.Arguments.GetValue(1)!;
                    return invocation.CreateValueReturn(true, x, y, x + y);
                }

                return invocation.CreateReturn();
            }, IsMethod);
            ICalculator calculator = stunt.ToObject();

            calculator.TurnOn();
            calculator.TurnOn();

            int x = 1, y = 2;
            Assert.True(calculator.TryAdd(ref x, ref y, out var z));
            Assert.True(calculator.TryAdd(ref x, ref y, out z));
            Assert.Equal(3, z);

            Assert.Equal(4, calls);
        }

        [Fact]
        public void EachBuiltStuntRemembersItsOwnResults()
        {
            var builder = Stunt.Builder().Memoize().AddBehavior(Add, IsMethod);

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            Assert.Equal(3, first.Add(1, 2));
            // The second stunt does not see the first one's result.
            Assert.Equal(3, second.Add(1, 2));
            Assert.Equal(2, calls);

            Assert.Equal(3, first.Add(1, 2));
            Assert.Equal(3, second.Add(1, 2));
            Assert.Equal(2, calls);
        }
    }
}