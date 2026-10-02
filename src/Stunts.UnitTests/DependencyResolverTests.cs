using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class DependencyResolverTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ResolvesDependencyWithOrWithoutVersion(bool includeVersion)
        {
            var assembly = typeof(StuntGenerator).Assembly;
            DependencyResolver.AddSearchPath(Path.GetDirectoryName(assembly.Location));

            var resolved = Resolve(includeVersion ? assembly.FullName : assembly.GetName().Name);

            Assert.Same(assembly, resolved);
        }

        [Fact]
        public void DoesNotResolveIncompatibleMajorVersion()
        {
            var assembly = typeof(StuntGenerator).Assembly;
            DependencyResolver.AddSearchPath(Path.GetDirectoryName(assembly.Location));
            var name = assembly.GetName();
            name.Version = new Version(name.Version.Major + 1, 0, 0, 0);

            Assert.Null(Resolve(name.FullName));
        }

        [Fact]
        public void DoesNotResolveMissingSatelliteAssembly()
        {
            DependencyResolver.AddSearchPath(Path.GetDirectoryName(typeof(StuntGenerator).Assembly.Location));

            Assert.Null(Resolve("Stunts.CodeAnalysis.resources, Culture=es"));
        }

        static Assembly Resolve(string name)
            => (Assembly)typeof(DependencyResolver)
                .GetMethod("OnAssemblyResolve", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { null, new ResolveEventArgs(name) });
    }
}
