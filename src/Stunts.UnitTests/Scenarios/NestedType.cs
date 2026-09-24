#pragma warning disable CS0436
using Stunts;

namespace Scenarios.NestedType
{
    /// <summary>
    /// Nested types are fully supported.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var stunt = Stunt.Of<IFoo>()
                .AddBehavior(new DefaultValueBehavior());

            stunt.Do();
        }

        public interface IFoo
        {
            void Do();
        }
    }
}
