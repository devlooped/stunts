using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests
{
    public class ST022_SyntaxOnlyProcessorUsesSemantics
    {
        [Fact]
        public void DoesNotReportSyntaxAndTrackedContextMembers()
        {
            Assert.Empty(Analyze("""
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.CSharp;
                using Microsoft.CodeAnalysis.CSharp.Syntax;
                using Stunts;

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var suffix = context.NamingConvention.NameSuffix;
                        return context.Language == LanguageNames.CSharp ? new Rewriter(context, suffix).Visit(syntax) : syntax;
                    }

                    class Rewriter : CSharpSyntaxRewriter
                    {
                        readonly ProcessorContext context;
                        readonly string suffix;

                        public Rewriter(ProcessorContext context, string suffix) => (this.context, this.suffix) = (context, suffix);

                        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
                            => base.VisitClassDeclaration(node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.ValueText + suffix)));
                    }
                }
                """));
        }

        [Theory]
        [InlineData("context.Compilation.AssemblyName", "reads 'ProcessorContext.Compilation'")]
        [InlineData("context.ParseOptions.Language", "reads 'ProcessorContext.ParseOptions'")]
        [InlineData("context.AdditionalFiles.Length.ToString()", "reads 'ProcessorContext.AdditionalFiles'")]
        [InlineData("context.ToString()", "reads 'ProcessorContext.ToString'")]
        public void ReportsUntrackedContextMembers(string expression, string usage)
        {
            var diagnostic = Assert.Single(Analyze($$"""
                using Microsoft.CodeAnalysis;
                using Stunts;

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
                        => syntax.WithAdditionalAnnotations(new SyntaxAnnotation({{expression}}));
                }
                """));

            Assert.Equal(StuntDiagnostics.SyntaxOnlyProcessorUsesSemantics.Id, diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.StartsWith($"'Processor' is marked [SyntaxOnlyProcessor] but {usage}", diagnostic.GetMessage());
        }

        [Fact]
        public void ReportsSemanticModelsInNestedTypes()
        {
            var diagnostic = Assert.Single(Analyze("""
                using Microsoft.CodeAnalysis;
                using Microsoft.CodeAnalysis.CSharp;
                using Microsoft.CodeAnalysis.CSharp.Syntax;
                using Stunts;

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context) => new Rewriter().Visit(syntax);

                    class Rewriter : CSharpSyntaxRewriter
                    {
                        public SemanticModel Model { get; set; }

                        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
                            => Model.GetDeclaredSymbol(node) == null ? node : base.VisitClassDeclaration(node);
                    }
                }
                """));

            Assert.Contains("Rewriter.Model', which the stunt generation cache does not track", diagnostic.GetMessage());
        }

        [Fact]
        public void ReportsSymbols()
        {
            var diagnostic = Assert.Single(Analyze("""
                using Microsoft.CodeAnalysis;
                using Stunts;

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public INamedTypeSymbol Target { get; set; }

                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
                        => syntax.WithAdditionalAnnotations(new SyntaxAnnotation(Target?.Name));
                }
                """));

            Assert.Contains("Target", diagnostic.GetMessage());
        }

        [Fact]
        public void ReportsContextPassedOutsideProcessor()
        {
            var diagnostic = Assert.Single(Analyze("""
                using Microsoft.CodeAnalysis;
                using Stunts;

                static class Helper
                {
                    public static SyntaxNode Apply(SyntaxNode syntax, ProcessorContext context) => syntax;
                }

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context) => Helper.Apply(syntax, context);
                }
                """));

            Assert.Contains("passes 'ProcessorContext' to 'Helper.Apply", diagnostic.GetMessage());
        }

        [Fact]
        public void DoesNotReportBindingProcessors()
        {
            Assert.Empty(Analyze("""
                using Microsoft.CodeAnalysis;
                using Stunts;

                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Rewrite;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
                        => context.Compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax) == null ? syntax : syntax;
                }
                """));
        }

        [Fact]
        public void DoesNotReportNameOf()
        {
            Assert.Empty(Analyze("""
                using Microsoft.CodeAnalysis;
                using Stunts;

                [SyntaxOnlyProcessor]
                public class Processor : ISyntaxProcessor
                {
                    public string Language => LanguageNames.CSharp;
                    public ProcessorPhase Phase => ProcessorPhase.Fixup;

                    public SyntaxNode Process(SyntaxNode syntax, ProcessorContext context)
                        => syntax.WithAdditionalAnnotations(new SyntaxAnnotation(nameof(context.Compilation)));
                }
                """));
        }

        [Fact]
        public void BuiltInSyntaxOnlyProcessorsDoNotReport()
        {
            var folder = Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, "..", "Stunts.CompiledProxy", "Processors");
            var sources = Directory.EnumerateFiles(folder, "*.cs")
                .Select(file => (file, text: File.ReadAllText(file)))
                .Where(source => source.text.Contains("[SyntaxOnlyProcessor]"))
                .ToArray();

            Assert.NotEmpty(sources);
            var compilation = Compile(sources.Select(source => source.text).ToArray());

            // Internal helpers of the signed Stunts.CompiledProxy do not bind here, but the
            // processor surface the analysis checks does.
            var processor = compilation.GetTypeByMetadataName("Stunts.ISyntaxProcessor");
            Assert.NotNull(processor);
            var processors = compilation.SyntaxTrees
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(node => node.AttributeLists.ToString().Contains("SyntaxOnlyProcessor"))
                    .Select(node => compilation.GetSemanticModel(tree).GetDeclaredSymbol(node)!))
                .ToArray();
            Assert.Equal(sources.Length, processors.Length);
            Assert.All(processors, type => Assert.Contains(processor, type.AllInterfaces, SymbolEqualityComparer.Default));

            Assert.Empty(Analyze(compilation));
        }

        static Diagnostic[] Analyze(params string[] sources) => Analyze(Compile(sources));

        static Diagnostic[] Analyze(Compilation compilation)
        {
            var analyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SyntaxOnlyProcessorAnalyzer()));

            return analyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
                .Where(diagnostic => diagnostic.Id == StuntDiagnostics.SyntaxOnlyProcessorUsesSemantics.Id)
                .ToArray();
        }

        static Compilation Compile(params string[] sources)
            => CSharpCompilation.Create(
                // Built-in processors use internals of Stunts.CompiledProxy and Stunts.CodeAnalysis.
                "Stunts.UnitTests",
                sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"Test{index}.cs")),
                MetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        static IEnumerable<MetadataReference> MetadataReferences()
            => AppDomain.CurrentDomain.GetAssemblies()
                .Append(typeof(SyntaxOnlyProcessorAttribute).Assembly)
                .Append(typeof(CSharpCompilation).Assembly)
                .Distinct()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
    }
}
