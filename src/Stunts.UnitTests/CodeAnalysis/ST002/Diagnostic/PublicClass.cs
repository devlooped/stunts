using System;

namespace Stunts.UnitTests.CodeAnalysis.ST002.Diagnostic
{
    public partial class MyClass
    {
        public MyClass()
        {
            var stunt = Stunt.Of<BaseType, OtherBaseType>();

            Console.WriteLine(stunt);
        }
    }

    public class BaseType { }
    public class OtherBaseType { }
}
