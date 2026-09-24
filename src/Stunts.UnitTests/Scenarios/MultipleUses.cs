#pragma warning disable CS0436
using System;
using Stunts;
using Xunit;

namespace Scenarios.MultipleUses
{
    /// <summary>
    /// Multiple uses of the stunt factory method only result in one 
    /// such type being generated (IWO, no compilation errors because 
    /// of duplicate types.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var disposable = Stunt.Of<IDisposable>();
            var services = Stunt.Of<IServiceProvider>();

            Assert.NotNull(disposable);
            Assert.NotNull(services);

            Do();
        }

        public void Do()
        {
            var disposable2 = Stunt.Of<IDisposable>();
            var services2 = Stunt.Of<IServiceProvider>();

            Assert.NotNull(disposable2);
            Assert.NotNull(services2);
        }
    }
}
