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
            var stunt = Stunt.For<IFoo>()
                .AddBehavior(new DefaultValueBehavior()).ToObject();

            IFoo foo = stunt;

            foo.Do();
        }

        public interface IFoo
        {
            void Do();
        }
    }
}
