using Stunts;
using Xunit;

namespace Samples
{
    public class Debugging
    {
        /// <summary>
        /// Set a breakpoint in the last line of the method and inspect the 
        /// stunt instance to see how Stunt optimizes and improves the 
        /// troubleshooting and inspection of running stunts and their 
        /// invocations.
        /// </summary>
        [Fact]
        public void DebuggerDisplay()
        {
            var calc = Stunt.Of<ICalculator>();
            var recorder = new RecordingBehavior();

            calc.AddBehavior(recorder);
            calc.AddBehavior(new DefaultEqualityBehavior());

            calc.AddBehavior(
                (invocation, next) => invocation.CreateValueReturn(invocation.Arguments.Get<int>(0) + invocation.Arguments.Get<int>(1)),
                invocation => invocation.MethodBase.Name == nameof(ICalculator.Add),
                "Add");

            calc.AddBehavior(new DefaultValueBehavior());
            calc.GetHashCode();

            Assert.False(calc.Equals(new object()));
            Assert.Equal(5, calc.Add(3, 2));
            Assert.Equal(10, calc.Add(5, 5));

            calc.Store("Mem1", 50);
            calc.Recall("Mem1");

            var calls = recorder.Invocations;
        }
    }
}
