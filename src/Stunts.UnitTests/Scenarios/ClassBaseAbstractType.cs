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
            CalculatorBase calculator = stunt;

            Assert.Throws<NotImplementedException>(() => calculator.Mode = CalculatorMode.Scientific);
            Assert.Throws<NotImplementedException>(() => calculator.Mode);
            Assert.Throws<NotImplementedException>(() => calculator.TurnOn());
        }
    }
}
