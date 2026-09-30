#pragma warning disable CS0436
using Stunts;
using Xunit;

namespace Scenarios.RefReturnsOut
{
    interface IMemory
    {
        ref int Get(ref string name, out int count);
    }

    /// <summary>
    /// Ref returns works OOB with out parameters and default value behaviors.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.For<IMemory>();
            stunt.AddBehavior(new DefaultValueBehavior());

            IMemory memory = stunt.ToObject();

            var name = "foo";
            ref int value = ref memory.Get(ref name, out var _);

            Assert.Equal(0, value);
        }
    }
}
