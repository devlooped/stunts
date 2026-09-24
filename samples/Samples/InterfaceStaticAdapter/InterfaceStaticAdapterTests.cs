using System;
using Stunts;

namespace Samples
{
    /// <summary>
    /// This example showcases how you can create a behavior that forwards 
    /// calls from an interface to matching methods in a static class, without 
    /// having to create an implementation for each such abstraction you want 
    /// to provide.
    /// </summary>
    public class InterfaceStaticAdapterTests
    {
        // Not annotated with Fact since that prevents console output collection.
        // Use TestDriven.NET VS extension to run this test with the ad-hoc 
        // runner and see the actual forwarded calls to Console.
        // [Fact]
        public void Test()
        {
            var console = Stunt.Of<IConsole>()
                .AddBehavior(new InterfaceStaticAdapterBehavior(typeof(Console)));

            console.Write("Hello");
            console.WriteLine(" World!");

            console.ForegroundColor = ConsoleColor.Red;
            console.WriteLine("Red!");
            console.BackgroundColor = ConsoleColor.Yellow;
            console.WriteLine("Oh no, my eyes!");
            console.ResetColor();

            // NOTE: if any of the above calls had not been properly forwarded, 
            // a NotImplementedException would have been thrown :).
        }
    }

    public interface IConsole
    {
        ConsoleColor BackgroundColor { get; set; }
        ConsoleColor ForegroundColor { get; set; }
        void ResetColor();

        void Write(string value);
        void WriteLine(string value);
    }

}
