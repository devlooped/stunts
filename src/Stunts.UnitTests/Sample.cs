using System;
using Stunts.Sample;
using Xunit;

namespace Stunts.UnitTests
{
    public class Sample
    {
        public void CanUseManualStunt()
        {
            var stunt = new CalculatorStunt();
            var recorder = new RecordingBehavior();
            stunt.AddBehavior(recorder);

            var isOn = false;
            stunt.TurnedOn += (_, __) => isOn = true;

            stunt.TurnOn();

            Assert.True(isOn);
            Assert.Equal(3, stunt.Add(1, 2));
            Assert.Equal(default, stunt.Mode);

            stunt.Store("balance", 100);
            Assert.Equal(100, stunt.Recall("balance"));
            Assert.Equal(100, stunt["balance"]);

            stunt.Store("mem1", 0);
            Assert.Equal(0, stunt.Recall("mem1"));
            Assert.Equal(0, stunt["mem1"]);

            var x = 1;
            var y = 2;
            int? z = 0;
            Assert.True(stunt.TryAdd(ref x, ref y, out z));
            Assert.Equal(3, z);

            Console.WriteLine(recorder.ToString());
        }
    }
}