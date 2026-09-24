using System;
using System.Reflection;
using Stunts;
using Xunit;

namespace Samples
{
    /// <summary>
    /// Showcases how an arbitrary method can be annotated as a "stunt generator" that 
    /// will seamlessly extended default behaviors for new stunts.
    /// </summary>
    public class CustomGenerator
    {
        [Fact]
        public void RandomPing()
        {
            var ping = Randomizer.Of<IPing>();

            // Each execution results in a new random value.
            Assert.NotEqual(ping.Ping(), ping.Ping());
            Assert.NotEqual(ping.Ping(), ping.Ping());
        }
    }

    public interface IPing
    {
        int Ping();
    }

    public static class Randomizer
    {
        static readonly Random random = new Random();

        [StuntGenerator]
        public static T Of<T>()
            => Stunt.Of<T>().AddBehavior(
                (invocation, next) => invocation.CreateValueReturn(random.Next()),
                invocation => invocation.MethodBase is MethodInfo info && info.ReturnType == typeof(int));
    }
}
