using System;

namespace Stunts.UnitTests.CodeAnalysis.ST010.Diagnostic
{
    public class MyClass
    {
        public MyClass()
        {
            var stunt = Stunt.Of<Action, IDisposable>();

            Console.WriteLine(stunt);
        }
    }
}
