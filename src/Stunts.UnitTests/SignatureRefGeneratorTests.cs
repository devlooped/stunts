using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Stunts.UnitTests
{
    public class SignatureRefGeneratorTests
    {
        [Fact]
        public void DoesNotEmitWhenSpanIsMissing()
        {
            var compilation = CompilationWith();

            Assert.Empty(Generate(compilation));
        }

        [Fact]
        public void EmitsWhenSpanExists()
        {
            var compilation = CompilationWith(MetadataReference.CreateFromFile(typeof(System.Span<int>).Assembly.Location));

            var sources = Generate(compilation);

            Assert.Contains(sources, source => source.HintName == "SignatureRef.cs");
        }

        [Fact]
        public void DoesNotEmitWhenTypesAlreadyExist()
        {
            var compilation = CompilationWith(
                MetadataReference.CreateFromFile(typeof(System.Span<int>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(SignatureRefGeneratorTests).Assembly.Location));

            Assert.Empty(Generate(compilation));
        }

        [Fact]
        public void DoesNotEmitWhenCompileTimeStuntsAreDisabled()
        {
            var compilation = CompilationWith(MetadataReference.CreateFromFile(typeof(System.Span<int>).Assembly.Location));

            Assert.Empty(Generate(compilation, compileTimeStunts: false));
        }

        [Fact]
        public void DoesNotEmitWhenUnsafeBlocksAreDisabled()
        {
            var compilation = CompilationWith(MetadataReference.CreateFromFile(typeof(System.Span<int>).Assembly.Location));

            Assert.Empty(Generate(compilation, allowUnsafe: false));
        }

        [Fact]
        public void DoesNotEmitWhenPropertiesAreMissing()
        {
            var compilation = CompilationWith(MetadataReference.CreateFromFile(typeof(System.Span<int>).Assembly.Location));

            Assert.Empty(Run(compilation, options: null));
        }

        static CSharpCompilation CompilationWith(params MetadataReference[] references)
            => CSharpCompilation.Create(
                "SignatureRef",
                new[] { CSharpSyntaxTree.ParseText("class C {}") },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        static GeneratedSourceResult[] Generate(Compilation compilation, bool compileTimeStunts = true, bool allowUnsafe = true)
            => Run(compilation, new ConfigOptions(
                (BuildProperties.Name.EnableCompileTimeStunts, compileTimeStunts ? "true" : "false"),
                (BuildProperties.Name.AllowUnsafeBlocks, allowUnsafe ? "true" : "false")));

        static GeneratedSourceResult[] Run(Compilation compilation, AnalyzerConfigOptionsProvider? options)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new ISourceGenerator[] { new SignatureRefGenerator().AsSourceGenerator() },
                optionsProvider: options);

            driver = driver.RunGenerators(compilation);

            return driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
        }

        sealed class ConfigOptions : AnalyzerConfigOptionsProvider
        {
            readonly AnalyzerConfigOptions global;

            public ConfigOptions(params (string Key, string Value)[] values)
                => global = new Map(values);

            public override AnalyzerConfigOptions GlobalOptions => global;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Map.Empty;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Map.Empty;

            sealed class Map : AnalyzerConfigOptions
            {
                public static AnalyzerConfigOptions Empty { get; } = new Map();

                readonly Dictionary<string, string> values;

                public Map(params (string Key, string Value)[] values)
                    => this.values = values.ToDictionary(pair => pair.Key, pair => pair.Value);

                public override bool TryGetValue(string key, out string? value)
                    => values.TryGetValue(key, out value);
            }
        }
    }
}
