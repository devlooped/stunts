using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST009_UnsafeSignature
    {
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void ReportsWhenEitherFlagIsOff(bool compileTimeStunts, bool allowUnsafe)
        {
            var diagnostic = Assert.Single(Analyze(SpanStunt, compileTimeStunts, allowUnsafe));

            Assert.Equal(StuntDiagnostics.UnsafeSignature.Id, diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(
                "'Buffer.Sum' uses a ref struct or pointer and requires compile-time stunts and AllowUnsafeBlocks",
                diagnostic.GetMessage());
        }

        [Fact]
        public void DoesNotReportWhenBothFlagsAreOn()
        {
            Assert.Empty(Analyze(SpanStunt, compileTimeStunts: true, allowUnsafe: true));
        }

        [Fact]
        public void ReportsWhenPropertiesAreMissing()
        {
            var diagnostic = Assert.Single(Analyze(SpanStunt, options: null));

            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("Buffer.Sum", diagnostic.GetMessage());
        }

        [Fact]
        public void DoesNotReportRefReadonlyOfAnOrdinaryType()
        {
            Assert.Empty(Analyze("""
                using Stunts;

                public class Counter
                {
                    int value;
                    public virtual ref readonly int Current() => ref value;
                }

                public class Test
                {
                    public void Run() => Stunt.Of<Counter>();
                }
                """, compileTimeStunts: false, allowUnsafe: false));
        }

        [Fact]
        public void DoesNotReportAnOrdinaryInterface()
        {
            Assert.Empty(Analyze("""
                using Stunts;

                public interface ICounter
                {
                    int Next();
                }

                public class Test
                {
                    public void Run() => Stunt.Of<ICounter>();
                }
                """, compileTimeStunts: false, allowUnsafe: false));
        }

        [Fact]
        public void ReportsAPointerParameter()
        {
            var diagnostic = Assert.Single(Analyze("""
                using Stunts;

                public unsafe interface IPointers
                {
                    void Do(int* value);
                }

                public class Test
                {
                    public void Run() => Stunt.Of<IPointers>();
                }
                """, compileTimeStunts: true, allowUnsafe: false));

            Assert.Contains("IPointers.Do", diagnostic.GetMessage());
        }

        [Fact]
        public void ReportsARefStructParameter()
        {
            var diagnostic = Assert.Single(Analyze("""
                using Stunts;

                public ref struct Token
                {
                    public int Value;
                }

                public class Parser
                {
                    public virtual int Read(Token token) => token.Value;
                }

                public class Test
                {
                    public void Run() => Stunt.Of<Parser>();
                }
                """, compileTimeStunts: false, allowUnsafe: true));

            Assert.Contains("Parser.Read", diagnostic.GetMessage());
        }

        const string SpanStunt = """
            using System;
            using Stunts;

            public class Buffer
            {
                public virtual int Sum(Span<int> data) => 0;
            }

            public class Test
            {
                public void Run() => Stunt.Of<Buffer>();
            }
            """;

        static Diagnostic[] Analyze(string source, bool compileTimeStunts, bool allowUnsafe)
            => Analyze(source, new ConfigOptions(
                (BuildProperties.Name.EnableCompileTimeStunts, compileTimeStunts ? "true" : "false"),
                (BuildProperties.Name.AllowUnsafeBlocks, allowUnsafe ? "true" : "false")));

        static Diagnostic[] Analyze(string source, AnalyzerConfigOptionsProvider? options)
        {
            var compilation = CSharpCompilation.Create(
                "UnsafeSignature",
                new[]
                {
                    CSharpSyntaxTree.ParseText(source, path: "Test.cs"),
                    CSharpSyntaxTree.ParseText(File.ReadAllText("Stunt/Stunt.cs"), path: "Stunt.cs"),
                },
                MetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            var analyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new UnsafeSignatureAnalyzer()),
                options == null ? null : new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, options));

            return analyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
                .Where(diagnostic => diagnostic.Id == StuntDiagnostics.UnsafeSignature.Id)
                .ToArray();
        }

        static IEnumerable<MetadataReference> MetadataReferences()
            => AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));

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
