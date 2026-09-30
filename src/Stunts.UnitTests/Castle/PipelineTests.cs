using System;
using System.Collections.Generic;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Castle interceptor pipeline: order, short-circuit, proceed, retry,
    /// and per-method selection.
    /// </summary>
    public class PipelineTests : IRunnable
    {
        public void Run()
        {
            BehaviorsRunInOrderAroundTheTarget();
            BehaviorCanShortCircuit();
            BehaviorCanProceedTwice();
            AppliesToSelectsBehaviorsPerMethod();
            ExceptionFromTheTargetCanBeCaughtAndReplaced();
            InvocationExposesMethodArgumentsAndTarget();
        }

        public void BehaviorsRunInOrderAroundTheTarget()
        {
            var log = new List<string>();
            var stunt = Stunt.For<Counter>();
            stunt.AddBehavior((invocation, next) =>
            {
                log.Add("outer");
                var result = next(invocation, next);
                log.Add("outer-done");
                return result;
            }, invocation => !invocation.MethodBase.IsConstructor);
            stunt.AddBehavior((invocation, next) =>
            {
                log.Add("inner");
                var result = next(invocation, next);
                log.Add("inner-done");
                return result;
            }, invocation => !invocation.MethodBase.IsConstructor);

            Counter counter = stunt.ToObject();

            Assert.Equal(1, counter.Next());
            Assert.Equal(new[] { "outer", "inner", "inner-done", "outer-done" }, log);
        }

        public void BehaviorCanShortCircuit()
        {
            var reached = false;
            var stunt = Stunt.For<Counter>().AddBehavior((invocation, next) => invocation.CreateValueReturn(5));
            stunt.AddBehavior((invocation, next) =>
            {
                reached = true;
                return next(invocation, next);
            });

            Counter counter = stunt.ToObject();

            Assert.Equal(5, counter.Next());
            Assert.False(reached);
        }

        public void BehaviorCanProceedTwice()
        {
            var stunt = Stunt.For<Counter>().AddBehavior((invocation, next) =>
            {
                next(invocation, next);
                return next(invocation, next);
            }).ToObject();

            Counter counter = stunt;

            Assert.Equal(2, counter.Next());
            Assert.Equal(2, counter.Count);
        }

        public void AppliesToSelectsBehaviorsPerMethod()
        {
            var stunt = Stunt.For<Counter>();
            stunt.AddBehavior(
                (invocation, next) => invocation.CreateValueReturn(10),
                invocation => invocation.MethodBase.Name == nameof(Counter.Next));
            stunt.AddBehavior(
                (invocation, next) => invocation.CreateValueReturn(20),
                invocation => invocation.MethodBase.Name == nameof(Counter.Other));

            Counter counter = stunt.ToObject();

            Assert.Equal(10, counter.Next());
            Assert.Equal(20, counter.Other());
        }

        public void ExceptionFromTheTargetCanBeCaughtAndReplaced()
        {
            var stunt = Stunt.For<Counter>().AddBehavior((invocation, next) =>
            {
                try
                {
                    return next(invocation, next);
                }
                catch (InvalidOperationException exception)
                {
                    return invocation.CreateExceptionReturn(new ApplicationException("wrapped", exception));
                }
            }).ToObject();

            var exception = Assert.Throws<ApplicationException>(() => ((Counter)stunt).Boom());

            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }

        public void InvocationExposesMethodArgumentsAndTarget()
        {
            IMethodInvocation seen = null;
            var stunt = Stunt.For<Counter>().AddBehavior((invocation, next) =>
            {
                seen = invocation;
                return invocation.CreateValueReturn(0);
            }).ToObject();

            Counter counter = stunt;

            counter.Add(2, 3);

            Assert.Same(counter, seen.Target);
            Assert.Equal(nameof(Counter.Add), seen.MethodBase.Name);
            Assert.Equal(2, seen.Arguments.Get<int>("left"));
            Assert.Equal(3, seen.Arguments.Get<int>("right"));
        }

        public class Counter
        {
            public int Count { get; private set; }

            public virtual int Next()
            {
                Count++;
                return Count;
            }

            public virtual int Other() => 0;

            public virtual int Add(int left, int right) => left + right;

            public virtual void Boom() => throw new InvalidOperationException("nope");
        }
    }
}
