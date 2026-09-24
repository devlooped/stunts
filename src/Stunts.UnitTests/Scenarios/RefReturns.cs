#pragma warning disable CS0436
using Stunts;
using Xunit;

namespace Scenarios.RefReturns
{
    interface IMemory
    {
        ref int Get();
    }

    /// <summary>
    /// Ref returns works OOB with default value behaviors.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<IMemory>();
            stunt.AddBehavior(new DefaultValueBehavior());

            ref int value = ref stunt.Get();
            Assert.Equal(0, value);
        }
    }
}
