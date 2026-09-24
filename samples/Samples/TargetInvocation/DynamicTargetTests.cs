using Stunts;
using Xunit;

namespace Samples.TargetInvocation
{
    public class DynamicTargetTests
    {
        [Fact]
        public void InvokeReturningTarget()
        {
            var target = new Calculator();

            ICalculator calc = Stunt.Of<ICalculator>();
            var recorder = new RecordingBehavior();

            // By adding the recorder *after* the dynamic target, 
            // we can check if any calls where made to the stunt
            // instead of the target.
            calc.AddBehavior(new DynamicTargetBehavior(target))
                 .AddBehavior(recorder);

            var result = calc.Add(2, 3);

            // We recorded the call to the stunt, but the 
            // dynamic target behavior passed the call through
            // to the real calculator which did the math.
            Assert.Empty(recorder.Invocations);
            Assert.Equal(5, result);
        }

        [Fact]
        public void InvokeVoidTarget()
        {
            var target = new Calculator();
            var stunt = Stunt.Of<ICalculator>();
            var stuntRecorder = new RecordingBehavior();

            // By adding the recorder *after* the dynamic target, 
            // we can check if any calls where made to the stunt
            // instead of the target.
            stunt.AddBehavior(new DynamicTargetBehavior(target))
                 .AddBehavior(stuntRecorder);

            stunt.Store("m1", 42);
            Assert.Equal(42, stunt.Recall("m1"));

            stunt.Clear("m1");
            Assert.Null(stunt.Recall("m1"));
        }
    }
}
