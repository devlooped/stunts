using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sample;

namespace Stunts.UnitTests
{
    class ManualStunts
    {
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Intended to be run by the ad-hoc TD.NET runner
        public void UpdateStunts()
        {
            if (!Debugger.IsAttached)
                throw new InvalidOperationException("This is intended to be run with the debugger attached.");

            var code = @"
using System;
using Stunts;
using Sample;

namespace UnitTests
{
    public class Test
    {
        public void Do()
        {
             _ = Stunt.Of<ICalculator>();
             _ = Stunt.Of<ICalculator, IDisposable>();
             _ = Stunt.Of<Calculator>();
             _ = Stunt.Of<CalculatorBase>();
             _ = Stunt.Of<ICalculatorMemory>();
             _ = Stunt.Of<CalculatorMemory>();
             _ = Stunt.Of<CalculatorMemoryBase>();
        }
    }
}";

            var libs = new HashSet<string>(File.ReadAllLines("lib.txt"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => Path.GetFileName(x));

            var args = CSharpCommandLineParser.Default.Parse(
                File.ReadAllLines("csc.txt"), ThisAssembly.Project.MSBuildProjectDirectory, sdkDirectory: null);

            var syntaxTree = CSharpSyntaxTree.ParseText(
                code,
                options: args.ParseOptions.WithLanguageVersion(LanguageVersion.Latest),
                path: Path.GetTempFileName(),
                encoding: Encoding.UTF8);

            var sources = new List<SyntaxTree>
            {
                syntaxTree
            };

            var additionalSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Stunt.cs",
                "Stunt.StaticFactory.cs"
            };

            foreach (var source in args.SourceFiles.Where(x => additionalSources.Contains(Path.GetFileName(x.Path))))
            {
                var filePath = source.Path;
                var fileName = filePath.StartsWith(ThisAssembly.Project.MSBuildProjectDirectory) ?
                    filePath.Substring(ThisAssembly.Project.MSBuildProjectDirectory.Length).TrimStart(Path.DirectorySeparatorChar) :
                    filePath;

                sources.Add(CSharpSyntaxTree.ParseText(
                    File.ReadAllText(filePath),
                    options: args.ParseOptions.WithLanguageVersion(LanguageVersion.Latest),
                    path: filePath,
                    encoding: Encoding.UTF8));
            }

            foreach (var thisAssemblyFile in Directory.EnumerateFiles(
                Path.Combine(
                    ThisAssembly.Project.MSBuildProjectDirectory,
                    ThisAssembly.Project.IntermediateOutputPath,
                    "generated"),
                "ThisAssembly.*.cs",
                SearchOption.AllDirectories))
            {
                sources.Add(CSharpSyntaxTree.ParseText(
                    File.ReadAllText(thisAssemblyFile),
                    options: args.ParseOptions.WithLanguageVersion(LanguageVersion.Latest),
                    path: thisAssemblyFile,
                    encoding: Encoding.UTF8));
            }

            Compilation compilation = CSharpCompilation.Create(
                "ManualStunts",
                sources,
                args.MetadataReferences.Select(x => libs.TryGetValue(Path.GetFileName(x.Reference), out var lib) ?
                    MetadataReference.CreateFromFile(lib) :
                    MetadataReference.CreateFromFile(x.Reference)),
                args.CompilationOptions.WithCryptoKeyFile(null).WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

            AssertCode.NoErrors(compilation);

            Predicate<Diagnostic> ignored = d =>
                d.Severity == DiagnosticSeverity.Hidden ||
                d.Severity == DiagnosticSeverity.Info;

            var diagnostics = compilation.GetDiagnostics().RemoveAll(ignored);
            var options = EditorConfigOptionsProvider.Create(Directory.EnumerateFiles(
                    Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, ThisAssembly.Project.IntermediateOutputPath),
                    "*.editorconfig", SearchOption.TopDirectoryOnly));

            var driver = CSharpGeneratorDriver.Create(
                new[] { new StuntGenerator() },
                parseOptions: args.ParseOptions.WithLanguageVersion(LanguageVersion.Latest),
                optionsProvider: options);

            // Don't timeout if we're debugging.
            var token = Debugger.IsAttached ? default : new CancellationTokenSource(5000).Token;

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out diagnostics, token);
            diagnostics = diagnostics.RemoveAll(ignored);

            AssertCode.NoErrors(compilation);

            // Copy from intermediate output to manual stunts folder.
            var generatedDir = Path.Combine(
                ThisAssembly.Project.MSBuildProjectDirectory,
                ThisAssembly.Project.IntermediateOutputPath,
                "generated",
                nameof(StuntGenerator));

            var names = new[]
            {
                StuntNaming.GetName(typeof(ICalculator)) + ".cs",
                StuntNaming.GetName(typeof(ICalculator), typeof(IDisposable)) + ".cs",
                StuntNaming.GetName(typeof(Calculator)) + ".cs",
                StuntNaming.GetName(typeof(CalculatorBase)) + ".cs",
                StuntNaming.GetName(typeof(ICalculatorMemory)) + ".cs",
                StuntNaming.GetName(typeof(CalculatorMemory)) + ".cs",
                StuntNaming.GetName(typeof(CalculatorMemoryBase)) + ".cs",
            };

            foreach (var name in names)
            {
                File.Copy(
                    Path.Combine(generatedDir, name),
                    Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, @"..\ManualStunts\Stunts", name),
                    true);
            }
        }
    }
}
