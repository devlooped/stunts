#pragma warning disable CS0436
using Stunts;
using Xunit;

namespace Scenarios.RefReturnsValue
{
    interface IMemory
    {
        ref int Get();
    }

    /// <summary>
    /// Can assign ref value and change it.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<IMemory>();
            Ref<int> original = 12;

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(original));

            ref int value = ref stunt.Get();
            value = 42;

            // Original value changes too :)
            // Can implicitly assign the Ref<T> to a T 
            int actual = original;
            Assert.Equal(42, actual);
            Assert.Equal(42, original.Value);
        }
    }
}
