#pragma warning disable CS0436
using System;
using Stunts;
using Xunit;


namespace Scenarios.MultipleInterfaces
{
    public class BaseType
    {
        public virtual void Run() { }
    }

    /// <summary>
    /// Can generate stunts for different interfaces implemented by 
    /// types, and there is no collision between them.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            var a1 = Stunt.Of<BaseType>();
            var a2 = Stunt.Of<BaseType, IDisposable>();
            var a3 = Stunt.Of<BaseType, IDisposable, IServiceProvider>();

            Assert.False(a1 is IDisposable || a1 is IServiceProvider);
            Assert.True(a2 is IDisposable && !(a1 is IServiceProvider));
            Assert.True(a3 is IDisposable && a3 is IServiceProvider);
        }
    }
}
