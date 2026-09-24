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


            var calculator = (ICalculator)factory.CreateStunt(Assembly.GetExecutingAssembly(),
                typeof(ICalculator), Array.Empty<Type>(), Array.Empty<object>());

            var recorder = new RecordingBehavior();
            calculator.AddBehavior(recorder);

            calculator.AddBehavior(
                (m, n) => new MethodReturn(m, "foo", m.Arguments),
                m => m.MethodBase.Name == "ToString",
                "ToString");

            calculator.AddBehavior(
                (m, n) => new MethodReturn(m, 42, m.Arguments),
                m => m.MethodBase.Name == "GetHashCode",
                "GetHashCode");

            calculator.AddBehavior(
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
        public void ConstructorInterceptionNotSupported()
        {
            BehaviorPipelineFactory.LocalDefault = new RecordingBehaviorPipelineFactory();
            StuntFactory.LocalDefault = new DynamicStuntFactory();

            var calculator = Stunt.Of<ICalculator>();
            var stunt = calculator as IStunt;

            Assert.NotNull(stunt);
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
