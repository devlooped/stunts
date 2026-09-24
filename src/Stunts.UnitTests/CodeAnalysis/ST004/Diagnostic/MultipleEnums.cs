using System;

namespace Stunts.UnitTests.CodeAnalysis.ST004.Diagnostic
{
    public partial class MultipleEnums
    {
        public MultipleEnums()
        {
            var stunt = Stunt.Of<PlatformID, TypeCode>();

            Console.WriteLine(stunt);
        }
    }
}
