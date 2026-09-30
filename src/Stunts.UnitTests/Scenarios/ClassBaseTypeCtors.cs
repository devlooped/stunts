#pragma warning disable CS0436
using Xunit;

namespace Stunts.Scenarios.ClassBaseTypeCtors
{
    public class BaseTypeCtor
    {
        public BaseTypeCtor(string name) : this(name, true) { }

        protected BaseTypeCtor(string name, bool enabled)
            => (Name, Enabled)
            = (name, enabled);

        public string Name { get; private set; }

        public bool Enabled { get; private set; }
    }

    public class Test : IRunnable
    {
        public void Run()
        {
            BaseTypeCtor instance = Stunt.Of<BaseTypeCtor>("Foo");

            Assert.Equal("Foo", instance.Name);
            Assert.True(instance.Enabled);

            instance = Stunt.Of<BaseTypeCtor>("Foo", false);

            Assert.Equal("Foo", instance.Name);
            Assert.False(instance.Enabled);
        }
    }
}