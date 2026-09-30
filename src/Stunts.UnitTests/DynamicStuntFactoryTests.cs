using System;
using System.Reflection;
using Sample;
using Xunit;

namespace Stunts.UnitTests
{
    public class DynamicStuntFactoryTests
    {
        public void test() => Console.WriteLine(typeof(DynamicStuntFactory).AssemblyQualifiedName);

        [Fact]
        public void TestFactory()
        {
            var factory = new DynamicStuntFactory();


            var stunt = new StuntReference<ICalculator>((ICalculator)factory.CreateStunt(Assembly.GetExecutingAssembly(),
                typeof(ICalculator), Array.Empty<Type>(), Array.Empty<object>()));

            ICalculator calculator = stunt.ToObject();

            var recorder = new RecordingBehavior();
            stunt.AddBehavior(recorder);

            stunt.AddBehavior(
                (m, n) => new MethodReturn(m, "foo", m.Arguments),
                m => m.MethodBase.Name == "ToString",
                "ToString");

            stunt.AddBehavior(
                (m, n) => new MethodReturn(m, 42, m.Arguments),
                m => m.MethodBase.Name == "GetHashCode",
                "GetHashCode");

            stunt.AddBehavior(
                (m, n) => new MethodReturn(m, true, m.Arguments),
                m => m.MethodBase.Name == "Equals",
                "Equals");

            Assert.Equal("foo", calculator.ToString());
            Assert.Equal(42, calculator.GetHashCode());
            Assert.True(calculator.Equals("foo"));
            Assert.Throws<NotImplementedException>(() => calculator.Add(2, 3));

            Assert.Equal(4, recorder.Invocations.Count);
        }

        [Fact]
        public void DelegateForwardsToTheImplementation()
        {
            var factory = new DynamicStuntFactory();
            var called = false;
            Action implementation = () => called = true;

            var action = (Action)factory.CreateStunt(
                Assembly.GetExecutingAssembly(),
                typeof(Action),
                Array.Empty<Type>(),
                new object[] { implementation });

            action();

            Assert.True(called);
        }

        [Fact]
        public void DelegateWithoutImplementationThrows()
        {
            var factory = new DynamicStuntFactory();
            var action = (Action)factory.CreateStunt(
                Assembly.GetExecutingAssembly(),
                typeof(Action),
                Array.Empty<Type>(),
                Array.Empty<object>());

            Assert.Throws<NotImplementedException>(() => action());
        }

        [Fact]
        public void ConstructorInterceptionNotSupported()
        {
            using var ambient = BehaviorPipelineFactory.UseAmbient(new RecordingBehaviorPipelineFactory());
            StuntFactory.LocalDefault = new DynamicStuntFactory();

            var stunt = Stunt.For<ICalculator>();
            _ = stunt.ToObject();

            Assert.Single(stunt.Behaviors);
            // Cannot record ctor call
            Assert.Empty(((RecordingBehavior)stunt.Behaviors[0]).Invocations);
        }

        class RecordingBehaviorPipelineFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline(new RecordingBehavior());
        }
    }
}
