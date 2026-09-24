#pragma warning disable CS0436
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Stunts;
using Xunit;

namespace Scenarios.CompilerGenerated
{
    public class BaseType { }

    /// <summary>
    /// Generated types have the [CompilerGenerated] attribute.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            Assert.NotNull(Stunt.Of<BaseType>().GetType().GetCustomAttribute<CompilerGeneratedAttribute>());
            Assert.NotNull(Stunt.Of<IDisposable>().GetType().GetCustomAttribute<CompilerGeneratedAttribute>());
        }
    }
}
