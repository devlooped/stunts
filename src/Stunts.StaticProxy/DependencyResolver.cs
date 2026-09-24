using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// Provides dependency resolution for the source generation process. 
    /// </summary>
    /// <remarks>
    /// This class is needed for cases where you want to have flexibility in how 
    /// your dependencies are resolved, without having to add them all as Analyzer 
    /// items, which might cause issues with Roslyn's built-in dependencies which 
    /// may not align with the ones you need.
    /// <para>
    /// The typical usage is to provide a `CompilerVisibleProperty` via targets in 
    /// your package that exposes to the source generator the path to your dependencies, 
    /// such as:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///   &lt;StuntsAnalyzerDir&gt;$(MSBuildThisFileDirectory)..\..\tools\netstandard2.0&lt;/StuntsAnalyzerDir&gt;
    /// &lt;/PropertyGroup&gt;
    /// 
    /// &lt;ItemGroup&gt;
    ///   &lt;CompilerVisibleProperty Include = "StuntsAnalyzerDir" / &gt;
    /// &lt;/ItemGroup&gt;
    /// </code>
    /// Then the source generator can access that property and register the search 
    /// path with:
    /// <code>
    /// public void Execute(GeneratorExecutionContext context)
    /// {
    /// 	if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.StuntsAnalyzerDir", out var analyerDir))
    /// 		DependencyResolver.AddSearchPath(analyerDir);
    /// 		
    /// 	...
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static class DependencyResolver
    {
        static HashSet<string> searchPaths = new();

        static DependencyResolver() => AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (searchPaths.Count == 0)
                return null;

            var requested = new AssemblyName(args.Name);
            if (requested.Name == null)
                return null;

            foreach (var dir in searchPaths)
            {
                var file = Path.GetFullPath(Path.Combine(dir, requested.Name + ".dll"));
                if (File.Exists(file))
                {
                    try
                    {
                        var actual = AssemblyName.GetAssemblyName(file);
                        // Only load compatible versions, allowing only minor version 
                        // mismatch.
                        if (actual.Version.Major == requested.Version.Major &&
                            actual.Version.Minor >= requested.Version.Minor)
                            return Assembly.LoadFrom(file);
                    }
                    catch (Exception e)
                    {
                        Debug.Fail($"Failed to load an assembly from '{file}'.", e.ToString());
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a directory to the <see cref="AppDomain.AssemblyResolve"/> probing paths when 
        /// loading the generator.
        /// </summary>
        /// <returns>Whether the directory was added or it was already registered.</returns>
        public static bool AddSearchPath(string path) => searchPaths.Add(path);
    }
}
