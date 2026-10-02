using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Stunts.CodeAnalysis;
using Xunit;

namespace Stunts.UnitTests;

public class ST012_InaccessibleInterfaceMember
{
    const string Contract = "public interface IContract { object Subject { get; internal set; } }";

    [Theory]
    [InlineData("public object Subject { get; }", "CS0535")]
    [InlineData("object IContract.Subject { get; set; }", "CS0122")]
    public void CompilerRejectsBothWaysToImplementExternalInternalSetter(string property, string expected)
    {
        var compilation = Consumer($"public class Implementation : IContract {{ {property} }}");

        Assert.Contains(compilation.GetDiagnostics(), diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id == expected);
    }

    [Fact]
    public void ReportsExternalInternalSetter()
    {
        var compilation = Consumer(Factory("IContract"));
        var diagnostic = Assert.Single(Analyze(compilation));

        Assert.Equal("ST012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("IContract.Subject.set", diagnostic.GetMessage());
    }

    [Fact]
    public void ReportsInheritedInternalSetter()
    {
        var compilation = Consumer(Factory("IDerived"), Contract + " public interface IDerived : IContract { }");

        Assert.Single(Analyze(compilation));
    }

    [Fact]
    public void AllowsSetterInSameAssembly()
    {
        var compilation = Consumer(Contract + Factory("IContract"), "public class Unrelated { }");

        Assert.Empty(Analyze(compilation));
    }

    [Fact]
    public void AllowsFriendAssembly()
    {
        var compilation = Consumer(Factory("IContract"),
            "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Consumer\")]\n" + Contract);

        Assert.Empty(Analyze(compilation));
    }

    [Fact]
    public void AllowsExistingClassImplementation()
    {
        var compilation = Consumer(Factory("Existing"),
            Contract + " public class Existing : IContract { public object Subject { get; set; } }");

        Assert.Empty(Analyze(compilation));
    }

    static string Factory(string type) => $$"""
        public static class Factory
        {
            [Stunts.StuntGenerator]
            public static T Create<T>() => default;
            public static object Run() => Create<{{type}}>();
        }
        """;

    static Diagnostic[] Analyze(Compilation compilation)
        => compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ValidateTypesAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().ToArray();

    static CSharpCompilation Consumer(string source, string contract = Contract)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var dependency = CSharpCompilation.Create("Contracts", new[] { CSharpSyntaxTree.ParseText(contract) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = dependency.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        return CSharpCompilation.Create("Consumer", new[] { CSharpSyntaxTree.ParseText(source) },
            references.Append(MetadataReference.CreateFromImage(stream.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
