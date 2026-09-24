using System;

namespace Stunts.UnitTests.CodeAnalysis.ST005.Diagnostic
{
    public partial class MyClass
    {
        public MyClass()
        {
            var stunt = Stunt.Of<IPointers>();

            Console.WriteLine(stunt);
        }
    }

    public interface IPointers
    {
        unsafe void Do(int* value);
    }
}
