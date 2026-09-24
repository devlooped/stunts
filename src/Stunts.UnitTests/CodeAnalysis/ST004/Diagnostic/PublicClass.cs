using System;

namespace Stunts.UnitTests.CodeAnalysis.ST004.Diagnostic
{
    public partial class MyClass
    {
        public MyClass()
        {
            var stunt = Stunt.Of<PlatformID>();

            Console.WriteLine(stunt);
        }
    }
}
