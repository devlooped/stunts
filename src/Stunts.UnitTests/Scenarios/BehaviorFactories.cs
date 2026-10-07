#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Linq;
using Xunit;

namespace Stunts.Scenarios.Factories
{
    public interface ICalculator
    {
        int Add(int x, int y);
    }

    public class CountingBehavior : IStuntBehavior
    {
        public int Calls;

        public bool AppliesTo(IMethodInvocation invocation) => true;

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            Calls++;
            return next(invocation, next);
        }
    }

    public class CloneableCountingBehavior : CountingBehavior, ICloneable
    {
        public object Clone() => new CloneableCountingBehavior();
    }

    public class BadCloneableBehavior : IStuntBehavior, ICloneable
    {
        public bool AppliesTo(IMethodInvocation invocation) => true;

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            => next(invocation, next);

        public object Clone() => new object();
    }

    /// <summary>
    /// A builder can register behavior factories so each built stunt gets its own
    /// behavior instances, and ICloneable behaviors are cloned per built stunt.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            FactoryCreatesDistinctInstancesPerBuild();
            FactoryStateIsNotSharedAcrossBuilds();
            CloneableBehaviorIsClonedPerBuild();
            PlainBehaviorsAreStillSharedAcrossBuilds();
            FactoryKeepsRegistrationOrder();
            InsertedFactoryKeepsPosition();
            NullFactoryThrows();
            NullFactoryResultThrows();
            BadCloneableThrows();
            FactoryOnLivePipelineInvokesImmediately();
            FactoryOnConstructedStuntInvokesImmediately();
        }

        public void FactoryCreatesDistinctInstancesPerBuild()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(() => new CountingBehavior());
            builder.AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            var firstBehavior = Stunt.Get(first).Behaviors.OfType<CountingBehavior>().Single();
            var secondBehavior = Stunt.Get(second).Behaviors.OfType<CountingBehavior>().Single();

            Assert.NotSame(firstBehavior, secondBehavior);
        }

        public void FactoryStateIsNotSharedAcrossBuilds()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(() => new CountingBehavior());
            builder.AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            var firstBehavior = Stunt.Get(first).Behaviors.OfType<CountingBehavior>().Single();
            var secondBehavior = Stunt.Get(second).Behaviors.OfType<CountingBehavior>().Single();

            Assert.NotSame(firstBehavior, secondBehavior);

            // Building already invokes the pipeline (constructor), so compare relative to baseline.
            var firstCalls = firstBehavior.Calls;
            var secondCalls = secondBehavior.Calls;

            first.Add(1, 2);

            Assert.Equal(firstCalls + 1, firstBehavior.Calls);
            Assert.Equal(secondCalls, secondBehavior.Calls);
        }

        public void CloneableBehaviorIsClonedPerBuild()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(new CloneableCountingBehavior());
            builder.AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            var firstBehavior = Stunt.Get(first).Behaviors.OfType<CloneableCountingBehavior>().Single();
            var secondBehavior = Stunt.Get(second).Behaviors.OfType<CloneableCountingBehavior>().Single();

            Assert.NotSame(firstBehavior, secondBehavior);

            // Building already invokes the pipeline (constructor), so compare relative to baseline.
            var firstCalls = firstBehavior.Calls;
            var secondCalls = secondBehavior.Calls;

            first.Add(1, 2);

            Assert.Equal(firstCalls + 1, firstBehavior.Calls);
            Assert.Equal(secondCalls, secondBehavior.Calls);
        }

        public void PlainBehaviorsAreStillSharedAcrossBuilds()
        {
            var shared = new CountingBehavior();
            var builder = Stunt.Builder()
                .AddBehavior(shared)
                .AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            Assert.Same(shared, Stunt.Get(first).Behaviors.OfType<CountingBehavior>().Single());
            Assert.Same(shared, Stunt.Get(second).Behaviors.OfType<CountingBehavior>().Single());
        }

        public void FactoryKeepsRegistrationOrder()
        {
            var first = new DefaultValueBehavior();
            var last = new RecordingBehavior();
            var builder = Stunt.Builder();
            builder.AddBehavior(first);
            builder.AddBehavior(() => new CountingBehavior());
            builder.AddBehavior(last);

            ICalculator calculator = builder.Build<ICalculator>();

            var behaviors = Stunt.Get(calculator).Behaviors;
            Assert.Same(first, behaviors[0]);
            Assert.IsType<CountingBehavior>(behaviors[1]);
            Assert.Same(last, behaviors[2]);
        }

        public void InsertedFactoryKeepsPosition()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(new DefaultValueBehavior());
            builder.InsertBehavior(0, () => new CountingBehavior());

            ICalculator calculator = builder.Build<ICalculator>();

            var behaviors = Stunt.Get(calculator).Behaviors;
            Assert.IsType<CountingBehavior>(behaviors[0]);
            Assert.IsType<DefaultValueBehavior>(behaviors[1]);
        }

        public void NullFactoryThrows()
        {
            var builder = Stunt.Builder();
            Assert.Throws<ArgumentNullException>(() => builder.AddBehavior((Func<IStuntBehavior>)null!));
        }

        public void NullFactoryResultThrows()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(() => (IStuntBehavior)null!);

            Assert.Throws<InvalidOperationException>(() => builder.Build<ICalculator>());
        }

        public void BadCloneableThrows()
        {
            var builder = Stunt.Builder();
            builder.AddBehavior(new BadCloneableBehavior());

            Assert.Throws<InvalidOperationException>(() => builder.Build<ICalculator>());
        }

        public void FactoryOnLivePipelineInvokesImmediately()
        {
            var stunt = Stunt.For<ICalculator>();
            stunt.AddBehavior(new DefaultValueBehavior());
            ICalculator calculator = stunt.ToObject();

            var calls = 0;
            stunt.InsertBehavior(0, () => { calls++; return new CountingBehavior(); });

            // Factory was invoked right away; the instance went into the live pipeline.
            Assert.Equal(1, calls);
            Assert.Equal(2, stunt.Behaviors.Count);
            Assert.IsType<CountingBehavior>(stunt.Behaviors[0]);

            calculator.Add(1, 2);
            Assert.Equal(1, stunt.Behaviors.OfType<CountingBehavior>().Single().Calls);
        }

        public void FactoryOnConstructedStuntInvokesImmediately()
        {
            var builder = Stunt.Builder().AddBehavior(new DefaultValueBehavior());
            ICalculator calculator = builder.Build<ICalculator>();

            var calls = 0;
            var reference = Stunt.Get(calculator);
            reference.InsertBehavior(0, () => { calls++; return new CountingBehavior(); });

            Assert.Equal(1, calls);
            var counting = reference.Behaviors.OfType<CountingBehavior>().Single();

            calculator.Add(1, 2);
            Assert.Equal(1, counting.Calls);
        }
    }
}
