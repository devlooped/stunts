using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests
{
    /// <summary>
    /// Replaces the global default factory, which every other test creating stunts reads,
    /// so it runs after the parallel tests, alone.
    /// </summary>
    [CollectionDefinition(nameof(GlobalStuntFactory), DisableParallelization = true)]
    public class GlobalStuntFactory { }

    [Collection(nameof(GlobalStuntFactory))]
    public class StuntFactoryTests
    {
        [Fact]
        public void NotImplementedFactoryThrows()
            => Assert.Throws<NotImplementedException>(() => StuntFactory.NotImplemented.CreateStunt(Assembly.GetExecutingAssembly(), typeof(IDisposable), Array.Empty<Type>(), Array.Empty<object>()));

        [Fact]
        public void ReplaceDefaultFactory()
        {
            var instance = new object();
            var factory = new TestFactory(instance);
            var original = StuntFactory.Default;

            StuntFactory.Default = factory;
            try
            {
                var actual = StuntFactory.Default.CreateStunt(
                    Assembly.GetExecutingAssembly(),
                    typeof(object),
                    new[] { typeof(IFormatProvider) },
                    Array.Empty<object>());

                Assert.Same(instance, actual);
            }
            finally
            {
                StuntFactory.Default = original;
            }
        }

        [Fact]
        public async Task ReplaceFactoryLocally()
        {
            var factory1 = new TestFactory(new object());
            var factory2 = new TestFactory(new object());

            await Task.WhenAll(
                Task.Run(() =>
                {
                    StuntFactory.LocalDefault = factory1;
                    Thread.Sleep(50);
                    Assert.Same(factory1, StuntFactory.Default);
                }),
                Task.Run(() =>
                {
                    StuntFactory.LocalDefault = factory2;
                    Thread.Sleep(50);
                    Assert.Same(factory2, StuntFactory.Default);
                })
            );

            Assert.NotSame(factory1, StuntFactory.Default);
            Assert.NotSame(factory2, StuntFactory.Default);
        }



        public class TestFactory : IStuntFactory
        {
            object instance;

            public TestFactory(object instance) => this.instance = instance;

            public object CreateStunt(Assembly assembly, Type baseType, Type[] implementedInterfaces, object?[] constructorArguments)
                => instance;
        }
    }
}
