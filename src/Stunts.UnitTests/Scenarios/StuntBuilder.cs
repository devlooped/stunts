#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.Reflection;
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

    public class NamedGreeter : Greeter
    {
        public override string Name() => "target";
    }

    public class Calculator : ICalculator, IDisposable
    {
        public Exception? Error { get; set; }

        public int Adds { get; private set; }

        public virtual int Add(int x, int y)
        {
            Adds++;
            if (Error != null)
                throw Error;

            return x + y;
        }

        public void Dispose() { }
    }

    public class Scientific : Calculator
    {
        public override int Add(int x, int y) => x * y;
    }

    public class OnlyCalc : ICalculator
    {
        public int Add(int x, int y) => x * y;
    }

    public interface IMemory
    {
        bool TryStore(ref int x, out int y);
    }

    public class Memory : IMemory
    {
        public bool TryStore(ref int x, out int y)
        {
            y = x;
            x++;
            return true;
        }
    }

    public interface INamed
    {
        string Name { get; set; }
    }

    public class Person : INamed
    {
        public string Name { get; set; } = "";
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
            ForwardsToAnInstance();
            ForwardsRefOutAndProperties();
            FactoryRunsOncePerStunt();
            MatchesOneRegistration();
            ClassTargetFallsBackToTheBase();
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

        public void ForwardsToAnInstance()
        {
            var calculator = new Calculator();
            var builder = Stunt.Builder();

            Assert.Same(builder, builder.Forward(calculator));
            Assert.Empty(builder.Behaviors);

            ICalculator stunt = builder.Build<ICalculator>();

            Assert.Single(Stunt.Get(stunt).Behaviors);
            Assert.IsType<TargetBehavior>(Stunt.Get(stunt).Behaviors[0]);
            Assert.Equal(3, stunt.Add(1, 2));
            Assert.Equal(1, calculator.Adds);
            Assert.False(stunt.Equals(calculator));

            calculator.Error = new InvalidOperationException("boom");
            var thrown = Assert.Throws<InvalidOperationException>(() => stunt.Add(1, 2));
            Assert.Same(calculator.Error, thrown);

            var outer = Stunt.Builder()
                .AddBehavior((invocation, next) => invocation.CreateValueReturn(42))
                .Forward(new Calculator())
                .Build<ICalculator>();

            Assert.Equal(42, outer.Add(1, 2));
        }

        public void ForwardsRefOutAndProperties()
        {
            var memory = new Memory();
            IMemory stored = Stunt.Builder().Forward(memory).Build<IMemory>();
            var x = 2;

            Assert.True(stored.TryStore(ref x, out var y));
            Assert.Equal(3, x);
            Assert.Equal(2, y);

            var person = new Person();
            INamed named = Stunt.Builder().Forward(person).Build<INamed>();
            named.Name = "ada";

            Assert.Equal("ada", person.Name);
            Assert.Equal("ada", named.Name);
        }

        public void FactoryRunsOncePerStunt()
        {
            var created = new List<Calculator>();
            var builder = Stunt.Builder().Forward(() =>
            {
                var calculator = new Calculator();
                created.Add(calculator);
                return calculator;
            });

            ICalculator first = builder.Build<ICalculator>();
            ICalculator second = builder.Build<ICalculator>();

            Assert.Empty(created);
            Assert.Equal(3, first.Add(1, 2));
            Assert.Equal(3, first.Add(1, 2));
            Assert.Equal(7, second.Add(3, 4));
            Assert.Equal(2, created.Count);
            Assert.NotSame(created[0], created[1]);
            Assert.Equal(2, created[0].Adds);
            Assert.Equal(1, created[1].Adds);

            var shared = new Calculator();
            var runs = 0;
            var reused = Stunt.Builder().Forward(() =>
            {
                runs++;
                return shared;
            });
            ICalculator one = reused.Build<ICalculator>();
            ICalculator two = reused.Build<ICalculator>();
            Assert.Equal(3, one.Add(1, 2));
            Assert.Equal(7, two.Add(3, 4));
            Assert.Equal(2, runs);
            Assert.Equal(2, shared.Adds);

            var during = 0;
            Greeter greeter = Stunt.Builder().Forward(() =>
            {
                during++;
                return new NamedGreeter();
            }).Build<Greeter>();

            Assert.Equal("target", greeter.Seen);
            Assert.Equal("target", greeter.Name());
            Assert.Equal(1, during);
        }

        public void MatchesOneRegistration()
        {
            var calculator = new Calculator();
            var only = new OnlyCalc();
            var overlapping = Stunt.Builder().Forward(calculator).Forward(only);
            var ambiguous = Assert.Throws<AmbiguousMatchException>(() => overlapping.Build<ICalculator>());

            Assert.Contains(typeof(Calculator).ToString(), ambiguous.Message);
            Assert.Contains(typeof(OnlyCalc).ToString(), ambiguous.Message);

            ICalculator selected = overlapping.Build<ICalculator, IDisposable>();
            Assert.Equal(3, selected.Add(1, 2));
            Assert.Equal(1, calculator.Adds);

            var scientific = new Scientific();
            var hierarchy = Stunt.Builder().Forward(calculator).Forward(scientific);
            Assert.Throws<AmbiguousMatchException>(() => hierarchy.Build<Calculator>());
            Scientific precise = hierarchy.Build<Scientific>();
            Assert.Equal(2, precise.Add(1, 2));

            var duplicate = Stunt.Builder().Forward(new Calculator());
            var again = Assert.Throws<InvalidOperationException>(() => duplicate.Forward<ICalculator>(new Calculator()));
            Assert.Contains("is already registered.", again.Message);
            Assert.Throws<InvalidOperationException>(() => duplicate.Forward(() => new Calculator()));
            Assert.Throws<ArgumentNullException>(() => Stunt.Builder().Forward<Calculator>((Calculator)null!));
            Assert.Throws<ArgumentNullException>(() => Stunt.Builder().Forward<Calculator>((Func<Calculator>)null!));

            ICalculator typedFactory = Stunt.Builder().Forward<ICalculator>(() => new Calculator()).Build<ICalculator>();
            Assert.Equal(3, typedFactory.Add(1, 2));
            ICalculator extra = Stunt.Builder().Forward<ICalculator>(() => new Calculator()).Build<ICalculator, IDisposable>();
            Assert.Throws<NotImplementedException>(() => extra.Add(1, 2));

            ICalculator concrete = Stunt.Builder().Forward(() => new Calculator()).Build<ICalculator, IDisposable>();
            Assert.Equal(3, concrete.Add(1, 2));
            Assert.IsAssignableFrom<IDisposable>(concrete);

            var late = Stunt.Builder();
            ICalculator before = late.Build<ICalculator>();
            late.Forward(new Calculator());
            Assert.Throws<NotImplementedException>(() => before.Add(1, 2));
            Assert.Equal(3, late.Build<ICalculator>().Add(1, 2));
        }

        public void ClassTargetFallsBackToTheBase()
        {
            Greeter plain = Stunt.Builder().Build<Greeter>();
            Assert.Equal("base", plain.Seen);
            Assert.Equal("base", plain.Name());

            Greeter unrelated = Stunt.Builder().Forward(new Calculator()).Build<Greeter>();
            Assert.Equal("base", unrelated.Seen);
            Assert.Equal("base", unrelated.Name());

            var target = new NamedGreeter();
            Greeter forwarded = Stunt.Builder().Forward(target).Build<Greeter>();
            Assert.Equal("target", forwarded.Seen);
            Assert.Equal("target", forwarded.Name());
        }
    }
}
