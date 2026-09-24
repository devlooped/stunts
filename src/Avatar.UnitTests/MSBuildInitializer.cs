#if NET48
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Avatars.UnitTests
{
    static class MSBuildInitializer
    {
        [ModuleInitializer]
        internal static void Run()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var binPath = ThisAssembly.Project.MSBuildBinPath;
            Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath(binPath);
            // Set environment variables so SDKs can be resolved. 
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(binPath, "MSBuild.exe"), EnvironmentVariableTarget.Process);
        }

        static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            if (name == null)
                return null;

            var file = Path.Combine(ThisAssembly.Project.MSBuildBinPath, name + ".dll");
            if (name.StartsWith("Microsoft.Build") && File.Exists(file))
                return Assembly.LoadFrom(file);

            return null;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute { }
}

#endif