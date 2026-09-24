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
            var calculator = Stunt.Of<ICalculator, IDisposable>();

            var recorder = new RecordingBehavior();

            calculator.AddBehavior(recorder);
            calculator.AddBehavior(new DefaultValueBehavior());

            Assert.IsAssignableFrom<IDisposable>(calculator);

            Assert.Equal(0, calculator.Add(5, 10));
            Assert.Single(recorder.Invocations);

            Console.WriteLine(recorder.ToString());
        }
    }
}
