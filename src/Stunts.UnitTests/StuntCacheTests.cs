using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Stunts.UnitTests
{
    public sealed class StuntCacheTests : IDisposable
    {
        const string Marker = "// from the stunts cache";

        readonly string cacheDir = Path.Combine(Path.GetTempPath(), "stunts-cache-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public void ReusesStuntsWhenOnlyBodiesChange()
        {
            Generate(Calculator("int") + Other("1"));
            Assert.NotEqual(0, MarkEntries());

            var (diagnostics, compilation) = Generate(Calculator("int") + Other("2"));

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains(Marker, Stunt(compilation, "ICalculatorStunt"));
            Assert.Equal(3, Invoke(compilation));
        }

        [Fact]
        public void RegeneratesStuntsWhenSignaturesChange()
        {
            Generate(Calculator("int"));
            MarkEntries();

            var (diagnostics, compilation) = Generate(Calculator("long"));

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            var stunt = Stunt(compilation, "ICalculatorStunt");
            Assert.DoesNotContain(Marker, stunt);
            Assert.Contains("long Add(", stunt);

            // The previous shape is still cached.
            Assert.Contains(Marker, Stunt(Generate(Calculator("int")).Compilation, "ICalculatorStunt"));
        }

        [Fact]
        public void SharesStuntsAcrossProjects()
        {
            var core = Library("Core", "public interface ICalculator { int Add(int x, int y); }");
            var consumer = "using Stunts; public static class Test { public static ICalculator Create() => Stunt.Of<ICalculator>(); }";

            Generate(consumer, "FirstTests", core);
            MarkEntries();

            var (diagnostics, compilation) = Generate(consumer, "SecondTests", core);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains(Marker, Stunt(compilation, "ICalculatorStunt"));
            Assert.True(compilation.Emit(new MemoryStream()).Success);
        }

        [Fact]
        public void DistinguishesFriendAssemblies()
        {
            var core = Library("Core", """
                [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FriendTests")]
                public interface ICalculator { int Add(int x, int y); }
                """);
            var consumer = "using Stunts; public static class Test { public static ICalculator Create() => Stunt.Of<ICalculator>(); }";

            Generate(consumer, "OtherTests", core);
            MarkEntries();

            Assert.DoesNotContain(Marker, Stunt(Generate(consumer, "FriendTests", core).Compilation, "ICalculatorStunt"));
        }

        [Fact]
        public void RegistersCachedNestedAndDelegateStunts()
        {
            const string source = """
                using System;
                using Stunts;
                public partial class Outer
                {
                    class Hidden { public virtual int Value() => 1; }
                    public static object Create() => Stunt.Of<Hidden>();
                    public static Func<int, int> Delegate() => Stunt.Builder().Build<Func<int, int>>(value => value);
                }
                """;

            Generate(source, "NestedFirst");
            MarkEntries();

            var (diagnostics, compilation) = Generate(source, "NestedSecond");

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            var nested = Stunt(compilation, "HiddenStunt");
            Assert.Contains(Marker, nested);
            Assert.Contains("__RegisterStunt", nested);
            Assert.Contains(Marker, Stunt(compilation, "FuncOfInt32Int32Stunt"));

            var outer = compilation.Emit(false).GetType("Outer")!;
            Assert.IsAssignableFrom<IStunt>(outer.GetMethod("Create")!.Invoke(null, null));
            var function = Assert.IsType<Func<int, int>>(outer.GetMethod("Delegate")!.Invoke(null, null));
            Assert.IsAssignableFrom<IStunt>(function.Target);
        }

        [Fact]
        public void ReusesDefaultImplementations()
        {
            const string source = """
                using Stunts;
                public interface IGreeter
                {
                    string Name { get; }
                    string Greet() => "Hi " + Name;
                }
                public static class Test { public static IGreeter Create() => Stunt.Of<IGreeter>(); }
                """;

            Generate(source);
            MarkEntries();

            var (diagnostics, compilation) = Generate(source);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains(Marker, Stunt(compilation, "IGreeterStunt"));
            Assert.Contains(Marker, Stunt(compilation, "DefaultIGreeter"));
        }

        [Fact]
        public void ReplaysDiagnosticsOfCachedStunts()
        {
            const string source = """
                using Stunts;
                public interface IGeneric { T Get<T>(); }
                public static class Test { public static IGeneric Create() => Stunt.Of<IGeneric>(); }
                """;

            Assert.Equal("ST015", Assert.Single(Generate(source, aot: true).Diagnostics).Id);
            MarkEntries();

            var (diagnostics, compilation) = Generate(source, aot: true);

            Assert.Equal("ST015", Assert.Single(diagnostics).Id);
            Assert.Contains(Marker, Stunt(compilation, "IGenericStunt"));
        }

        [Fact]
        public void RegeneratesStuntsWhoseNamesCollide()
        {
            const string types = """
                using Stunts;
                namespace A { public class Bar { } }
                namespace B { public class Bar { } }
                public interface IBox<T>
                {
                    T Get();
                    string Name() => "box";
                }

                """;
            var single = types + "public static class Test { public static object Create() => Stunt.Of<IBox<A.Bar>>(); }";
            var both = types + "public static class Test { public static object[] Create() => new object[] { Stunt.Of<IBox<A.Bar>>(), Stunt.Of<IBox<B.Bar>>() }; }";

            Generate(single);
            MarkEntries();

            var (diagnostics, compilation) = Generate(both);

            Assert.Empty(diagnostics);
            Assert.True(compilation.Emit(new MemoryStream()).Success);
            Assert.DoesNotContain(compilation.SyntaxTrees, tree => tree.ToString().Contains(Marker));

            // The unique names did not replace the plain ones.
            Assert.Contains(Marker, Stunt(Generate(single).Compilation, "IBoxOfBarStunt"));
        }

        [Fact]
        public void RegeneratesStuntsForAnotherCulture()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("lt-LT");
                Generate(Calculator("int"));
                MarkEntries();

                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.DoesNotContain(Marker, Stunt(Generate(Calculator("int")).Compilation, "ICalculatorStunt"));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [Fact]
        public void DoesNotCacheWithCustomProcessors()
        {
            Generate(Calculator("int"), generator: new StuntGenerator().WithProcessor(new NoopProcessor()));

            Assert.False(Directory.Exists(cacheDir));
        }

        [Fact]
        public void DoesNotCacheWithoutDirectory()
        {
            Generate(Calculator("int"), cache: false);

            Assert.False(Directory.Exists(cacheDir));
        }

        [Fact]
        public void RejectsDamagedEntries()
        {
            var entry = new StuntCacheEntry(StuntCacheFlags.GenericMethods, new[] { "global::IFoo" }, "class Foo { }");
            var text = entry.Write();

            var read = StuntCacheEntry.Read(text)!;
            Assert.Equal(entry.Flags, read.Flags);
            Assert.Equal(entry.Defaults, read.Defaults);
            Assert.Equal(entry.Code, read.Code);
            Assert.Null(StuntCacheEntry.Read(text.Substring(0, text.Length - 1)));
            Assert.Null(StuntCacheEntry.Read("garbage"));
        }

        static string Calculator(string type) => $$"""
            using Stunts;
            public interface ICalculator { {{type}} Add(int x, int y); }
            public static class Test
            {
                public static int Run()
                {
                    var stunt = Stunt.For<ICalculator>();
                    ICalculator calculator = stunt.ToObject();
                    stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(({{type}})3));
                    return (int)calculator.Add(1, 2);
                }
            }
            """;

        static string Other(string value) => $$"""

            public static class Other { public static int Value() => {{value}}; }
            """;

        // Appends a marker to every cached source, which only a cache hit can emit.
        int MarkEntries()
        {
            StuntCache.ClearMemory();
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(cacheDir, "*.stunt", SearchOption.AllDirectories))
            {
                if (StuntCacheEntry.Read(File.ReadAllText(file)) is not { Code: string code } entry)
                    continue;

                File.WriteAllText(file, new StuntCacheEntry(entry.Flags, entry.Defaults, code + Marker + Environment.NewLine).Write());
                count++;
            }

            return count;
        }

        static string Stunt(Compilation compilation, string name)
            => compilation.SyntaxTrees
                .Single(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Any(type => type.Identifier.ValueText == name))
                .ToString();

        static int Invoke(Compilation compilation)
            => (int)compilation.Emit(false).GetType("Test")!.GetMethod("Run")!.Invoke(null, null)!;

        static MetadataReference Library(string name, string source)
        {
            var compilation = CSharpCompilation.Create(name,
                new[] { CSharpSyntaxTree.ParseText(source, ParseOptions()) },
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        (ImmutableArray<Diagnostic> Diagnostics, Compilation Compilation) Generate(string source, string? assemblyName = null,
            MetadataReference? reference = null, bool aot = false, bool cache = true, StuntGenerator? generator = null, [CallerMemberName] string? test = null)
        {
            var parseOptions = ParseOptions();
            var compilation = CSharpCompilation.Create(
                assemblyName ?? test,
                new[]
                {
                    CSharpSyntaxTree.ParseText(source, parseOptions, path: test + ".cs", encoding: Encoding.UTF8),
                    CSharpSyntaxTree.ParseText(File.ReadAllText("Stunt/Stunt.cs"), parseOptions, path: "Stunt.cs", encoding: Encoding.UTF8),
                    CSharpSyntaxTree.ParseText(File.ReadAllText("Stunt/Stunt.StaticFactory.cs"), parseOptions, path: "Stunt.StaticFactory.cs", encoding: Encoding.UTF8),
                },
                reference == null ? References() : References().Append(reference),
                CompilerArguments().CompilationOptions.WithCryptoKeyFile(null).WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

            var properties = new Dictionary<string, string>();
            if (cache)
                properties[BuildProperties.Name.StuntsCacheDir] = cacheDir;
            if (aot)
                properties[BuildProperties.Name.PublishAot] = "true";

            var optionsProvider = new PropertiesProvider(EditorConfigOptionsProvider.Create(Directory.EnumerateFiles(
                Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, ThisAssembly.Project.IntermediateOutputPath),
                "*.editorconfig", SearchOption.TopDirectoryOnly)), properties);

            CSharpGeneratorDriver.Create(
                    new ISourceGenerator[] { generator ?? new StuntGenerator(), new SignatureRefGenerator().AsSourceGenerator() },
                    parseOptions: parseOptions,
                    optionsProvider: optionsProvider)
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

            return (diagnostics.RemoveAll(d => d.Severity is DiagnosticSeverity.Hidden or DiagnosticSeverity.Info), output);
        }

        static CSharpCommandLineArguments CompilerArguments()
            => CSharpCommandLineParser.Default.Parse(File.ReadAllLines("csc.txt"), ThisAssembly.Project.MSBuildProjectDirectory, sdkDirectory: null);

        static CSharpParseOptions ParseOptions()
            => ((CSharpParseOptions)CompilerArguments().ParseOptions)
                .WithLanguageVersion(LanguageVersion.Latest)
                .WithFeatures(Enumerable.Empty<KeyValuePair<string, string>>());

        static IEnumerable<MetadataReference> References()
        {
            var libs = new HashSet<string>(File.ReadAllLines("lib.txt"), StringComparer.OrdinalIgnoreCase)
                .Distinct(FileNameEqualityComparer.Default)
                .ToDictionary(x => Path.GetFileName(x));

            return CompilerArguments().MetadataReferences
                .Select(x => libs.TryGetValue(Path.GetFileName(x.Reference), out var lib) ?
                    MetadataReference.CreateFromFile(lib) :
                    MetadataReference.CreateFromFile(x.Reference))
                .ToArray();
        }

        sealed class NoopProcessor : ISyntaxProcessor
        {
            public string Language => LanguageNames.CSharp;
            public ProcessorPhase Phase => ProcessorPhase.Fixup;
            public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context) => syntax;
        }

        sealed class PropertiesProvider : AnalyzerConfigOptionsProvider
        {
            readonly AnalyzerConfigOptionsProvider original;
            readonly AnalyzerConfigOptions options;

            public PropertiesProvider(AnalyzerConfigOptionsProvider original, Dictionary<string, string> properties)
                => (this.original, options) = (original, new Options(original.GlobalOptions, properties));

            public override AnalyzerConfigOptions GlobalOptions => options;
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => original.GetOptions(tree);
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => original.GetOptions(textFile);

            sealed class Options : AnalyzerConfigOptions
            {
                readonly AnalyzerConfigOptions original;
                readonly Dictionary<string, string> properties;

                public Options(AnalyzerConfigOptions original, Dictionary<string, string> properties)
                    => (this.original, this.properties) = (original, properties);

                public override bool TryGetValue(string key, out string value)
                    => properties.TryGetValue(key, out value!) || original.TryGetValue(key, out value!);
            }
        }
    }
}
