#pragma warning disable CS0436
using Stunts;
using Xunit;

public interface IGlobalNamespaceInterface
{
    void Run();
}

public class GlobalNamespaceClass
{
    public void Run() { }
}


namespace Scenarios.GlobalNamespace
{
    /// <summary>
    /// Basic interface implementation and behaviors are correct.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            Assert.NotNull(Stunt.Of<IGlobalNamespaceInterface>());
            Assert.NotNull(Stunt.Of<GlobalNamespaceClass>());
        }
    }
}
