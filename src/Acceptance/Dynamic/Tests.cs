using System;
using Stunts;
using Xunit;

namespace Sample
{
    public class Tests
    {
        [Fact]
        public void CanConfigureDefaultValues()
        {
            var stunt = Stunt.For<ICalculator, IDisposable>();
            ICalculator calculator = stunt.ToObject();

            Assert.IsNotType<CompiledStuntFactory>(StuntFactory.Default);

            var recorder = new RecordingBehavior();
            stunt.AddBehavior(recorder);
            stunt.AddBehavior(new DefaultValueBehavior());

            Assert.IsAssignableFrom<IDisposable>(calculator);

            Assert.Equal(0, calculator.Add(5, 10));
            Assert.Single(recorder.Invocations);

            Console.WriteLine(recorder.ToString());
        }
    }
}
