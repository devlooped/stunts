using System;

namespace Stunts.UnitTests.CodeAnalysis.ST001.Diagnostic
{
    public partial class MyClass
    {
        public MyClass()
        {
            var stunt = Stunt.Of<IDisposable, BaseType>();

            Console.WriteLine(stunt);
        }
    }

    public class BaseType { }
}
