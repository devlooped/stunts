#pragma warning disable CS0436
using System;
using Sample;
using Xunit;

namespace Stunts.Scenarios.ClassBaseAbstractType
{
    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<CalculatorBase>();

            Assert.Throws<NotImplementedException>(() => stunt.Mode = CalculatorMode.Scientific);
            Assert.Throws<NotImplementedException>(() => stunt.Mode);
            Assert.Throws<NotImplementedException>(() => stunt.TurnOn());
        }
    }
}
