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
            var stunt = Stunt.Of<BaseTypeCtor>("Foo");

            Assert.Equal("Foo", stunt.Name);
            Assert.True(stunt.Enabled);

            stunt = Stunt.Of<BaseTypeCtor>("Foo", false);

            Assert.Equal("Foo", stunt.Name);
            Assert.False(stunt.Enabled);
        }
    }
}