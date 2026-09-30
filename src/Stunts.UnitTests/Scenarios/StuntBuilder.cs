#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using Xunit;

namespace Stunts.Scenarios.Builders
{
    public interface ICalculator
    {
        int Add(int x, int y);
    }

    public class Greeter
    {
        public Greeter() => Seen = Name();

        public string Seen { get; }

        public virtual string Name() => "base";
    }

    public delegate int Adder(int x, int y);

    /// <summary>
    /// A builder applies the same behaviors to every stunt it builds, from 
    /// the very moment of their instantiation.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            BehaviorsApplyToBuiltStunts();
            BehaviorsApplyDuringConstruction();
            BehaviorsAreSharedAcrossBuilds();
            BehaviorsAreSnapshotAtBuildTime();
            BuildsAdditionalInterfaces();
            BuildsDelegates();
        }

        public void BehaviorsApplyToBuiltStunts()
        {
            var builder = Stunt.Builder();

            // The behavior extension methods preserve the builder type, so they can be chained.
            StuntBuilder chained = builder.AddBehavior(new DefaultValueBehavior());

            Assert.Same(builder, chained);

            ICalculator calculator = builder.Build<ICalculator>();

            // No post-creation configuration was needed for the behavior to apply.
            Assert.Equal(0, calculator.Add(1, 2));
            Assert.Single(Stunt.Get(calculator).Behaviors);
        }

        public void BehaviorsApplyDuringConstruction()
        {
            var builder = Stunt.Builder()
                .AddBehavior((invocation, next) => invocation.MethodBase.Name == nameof(Greeter.Name)
                    ? invocation.CreateValueReturn("proxy")
                    : next(invocation, next));

            Greeter greeter = builder.Build<Greeter>();

            // Something Stunt.Of cannot do, since the behavior would be added after the fact.
            Assert.Equal("proxy", greeter.Seen);
        }

        public void BehaviorsAreSharedAcrossBuilds()
        {
            var recorder = new RecordingBehavior();
            var builder = Stunt.Builder()
                .AddBehavior(recorder)
                .AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            first.Add(1, 2);
            second.Add(3, 4);

            Assert.NotSame(first, second);
            // Two constructor invocations plus the two Add calls.
            Assert.Equal(4, recorder.Invocations.Count);
        }

        public void BehaviorsAreSnapshotAtBuildTime()
        {
            var builder = Stunt.Builder().AddBehavior(new DefaultValueBehavior());

            ICalculator first = builder.Build<ICalculator>();

            builder.InsertBehavior(0, (invocation, next) => invocation.CreateValueReturn(42));

            ICalculator second = builder.Build<ICalculator>();

            Assert.Equal(2, builder.Behaviors.Count);
            Assert.Equal(0, first.Add(1, 2));
            Assert.Equal(42, second.Add(1, 2));
        }

        public void BuildsAdditionalInterfaces()
        {
            ICalculator calculator = Stunt.Builder()
                .AddBehavior(new DefaultValueBehavior())
                .Build<ICalculator, IDisposable>();

            Assert.IsAssignableFrom<IDisposable>(calculator);
            Assert.Equal(0, calculator.Add(1, 2));
        }

        public void BuildsDelegates()
        {
            var recorder = new RecordingBehavior();
            Adder adder = Stunt.Builder()
                .AddBehavior(recorder)
                .Build<Adder>((x, y) => x + y);

            Assert.Equal(3, adder(1, 2));
            Assert.Single(recorder.Invocations);
        }
    }
}
