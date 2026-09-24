using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sample;
using Stunts.CodeAnalysis;
using TypeNameFormatter;
using Xunit;

namespace Stunts.UnitTests
{
    public interface ITypeGetter
    {
        Type GetType(string assembly, string name);
    }

    public class BaseClass
    {
        public virtual bool TryMixed(int x, int? y, ref string name, out int? z)
        {
            z = x + y;
            return true;
        }
    }

    [CompilerGenerated]
    class BaseClassStunt : BaseClass, IStunt
    {
        readonly BehaviorPipeline pipeline = BehaviorPipelineFactory.Default.CreatePipeline<BaseClassStunt>();
        [CompilerGenerated]
        public BaseClassStunt() => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(this, m.Arguments)));
        [CompilerGenerated]
        IList<IStuntBehavior> IStunt.Behaviors => pipeline.Behaviors;
        [CompilerGenerated]
        public override bool Equals(object obj) => pipeline.Execute<bool>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.Equals(obj), obj), obj));
        [CompilerGenerated]
        public override int GetHashCode() => pipeline.Execute<int>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.GetHashCode())));
        [CompilerGenerated]
        public override string ToString() => pipeline.Execute<string>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (m, n) => m.CreateValueReturn(base.ToString())));
        [CompilerGenerated]
        public override bool TryMixed(int x, int? y, ref string name, out int? z)
        {
            var _method = MethodBase.GetCurrentMethod();
            z = default;
            var _result = pipeline.Invoke(MethodInvocation.Create(this, _method, (m, n) =>
            {
                var _name = m.Arguments.Get<string>("name");
                var _z = m.Arguments.Get<int?>("z");
                return m.CreateValueReturn(base.TryMixed(x, y, ref _name, out _z), new ArgumentCollection(_method.GetParameters())
                {{"x", x}, {"y", y}, {"name", _name}, {"z", _z}});
            }, x, y, name, z), true);
            x = _result.Outputs.Get<int>("x");
            y = _result.Outputs.Get<int?>("y");
            name = _result.Outputs.Get<string>("name");
            z = _result.Outputs.Get<int?>("z");
            return (bool)_result.ReturnValue!;
        }
    }

    public class StuntGeneratorTests
    {
        // NOTE: add more representative types here if needed when fixing codegen
        [InlineData(typeof(IDisposable), typeof(IServiceProvider), typeof(IFormatProvider))]
        [InlineData(typeof(ICollection<string>), typeof(IDisposable))]
        [InlineData(typeof(IDictionary<IReadOnlyCollection<string>, IReadOnlyList<int>>), typeof(IDisposable))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(CalculatorBase))]
        [InlineData(typeof(Calculator))]
        [InlineData(typeof(BaseClass))]
        [InlineData(typeof(INotifyPropertyChanged))]
        [InlineData(typeof(ICustomFormatter))]
        [InlineData(typeof(ITypeGetter))]
        [Theory]
        public void GenerateCode(params Type[] types)
        {
            var code = @"
using System;
using Stunts;

namespace UnitTests
{
    public class Test
    {
        public void Do()
        {
            var stunt = Stunt.Of<$$>();
            Console.WriteLine(stunt.ToString());
        }
    }
}".Replace("$$", string.Join(", ", types.Select(t =>
                     t.GetFormattedName(TypeNameFormatOptions.Namespaces))));

            var (diagnostics, compilation) = GetGeneratedOutput(code);

            Assert.Empty(diagnostics);

            var assembly = compilation.Emit(false);

            var name = StuntNaming.GetFullName(types.First(), types.Skip(1).ToArray());
            var type = assembly.GetType(name);

            Assert.NotNull(type);

            var stunt = Activator.CreateInstance(type!);

            foreach (var t in types)
            {
                Assert.IsAssignableFrom(t, stunt);
            }
        }

        [Fact]
        public void FailIfNoAvailableConstructor()
        {
            var code = @"
using System;
using Stunts;

namespace UnitTests
{
    public class Test
    {
        public void Do() => Stunt.Of<BaseTypePrivateCtor>();
    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code, new[]
            {
@"
    public class BaseTypePrivateCtor
    {
        private BaseTypePrivateCtor() { }
    }
"
            });

            Assert.Single(diagnostics);

            var diagnostic = diagnostics.First();

            Assert.Equal(StuntDiagnostics.BaseTypeNoContructor.Id, diagnostic.Id);
            Assert.Equal(8, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void SucceedsIfInternalConstructor()
        {
            var code = @"
using System;
using Stunts;

namespace UnitTests
{
    public class Test
    {
        public void Do() => Stunt.Of<BaseTypeInternalCtor>();
    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code, new[]
            {
@"
public class BaseTypeInternalCtor
{
    internal BaseTypeInternalCtor() { }
}
"
            });

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void SucceedsIfInternalProtectedConstructor()
        {
            var code = @"
using System;
using Stunts;

namespace UnitTests
{
    public class Test
    {
        public void Do() => Stunt.Of<BaseTypeInternalCtor>();
    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code, new[]
            {
@"
public class BaseTypeInternalCtor
{
    internal protected BaseTypeInternalCtor() { }
}
"
            });

            Assert.Empty(diagnostics);
        }

        static (ImmutableArray<Diagnostic>, Compilation) GetGeneratedOutput(string source, string[] additionalSources = null, [CallerMemberName] string? test = null)
        {
            var libs = new HashSet<string>(File.ReadAllLines("lib.txt"), StringComparer.OrdinalIgnoreCase)
                .Distinct(FileNameEqualityComparer.Default)
                .ToDictionary(x => Path.GetFileName(x));

            var args = CSharpCommandLineParser.Default.Parse(
                File.ReadAllLines("csc.txt"), ThisAssembly.Project.MSBuildProjectDirectory, sdkDirectory: null);

            // net10 csc passes /features:InterceptorsNamespaces. The generator builds its
            // trees with default parse options, and Roslyn refuses to mix those features.
            var parseOptions = args.ParseOptions
                .WithLanguageVersion(LanguageVersion.Latest)
                .WithFeatures(Enumerable.Empty<KeyValuePair<string, string>>());

            var sources = (additionalSources ?? Array.Empty<string>())
                .Select((code, index) => CSharpSyntaxTree.ParseText(
                    code,
                    options: parseOptions,
                    path: $"AdditionalSource{index}.cs",
                    encoding: Encoding.UTF8))
                .Concat(new[]
                {
                    CSharpSyntaxTree.ParseText(source, options: parseOptions, path: test + ".cs", encoding: Encoding.UTF8),
                    CSharpSyntaxTree.ParseText(File.ReadAllText("Stunt/Stunt.cs"), options: parseOptions, path: "Stunt.cs", encoding: Encoding.UTF8),
                    CSharpSyntaxTree.ParseText(File.ReadAllText("Stunt/Stunt.StaticFactory.cs"), options: parseOptions, path: "Stunt.StaticFactory.cs", encoding: Encoding.UTF8),
                });

            var references = args.MetadataReferences.Select(x => libs.TryGetValue(Path.GetFileName(x.Reference), out var lib) ?
                    MetadataReference.CreateFromFile(lib) :
                    MetadataReference.CreateFromFile(x.Reference))
                .ToList();

            // Types passed to GenerateCode (e.g. BaseClass) live in this assembly.
            var testAssembly = typeof(StuntGeneratorTests).Assembly.Location;
            if (!string.IsNullOrEmpty(testAssembly) &&
                !references.Any(r => string.Equals(r.Display, testAssembly, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(MetadataReference.CreateFromFile(testAssembly));
            }

            var compilation = CSharpCompilation.Create(
                test,
                sources,
                references,
                args.CompilationOptions.WithCryptoKeyFile(null).WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

            Predicate<Diagnostic> ignored = d =>
                d.Severity == DiagnosticSeverity.Hidden ||
                d.Severity == DiagnosticSeverity.Info;

            var diagnostics = compilation.GetDiagnostics().RemoveAll(ignored);
            if (diagnostics.Any())
                return (diagnostics, compilation);

            var driver = CSharpGeneratorDriver.Create(
                new[] { new StuntGenerator() },
                parseOptions: parseOptions,
                optionsProvider: EditorConfigOptionsProvider.Create(Directory.EnumerateFiles(
                    Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, ThisAssembly.Project.IntermediateOutputPath),
                    "*.editorconfig", SearchOption.TopDirectoryOnly)));

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out diagnostics);
            diagnostics = diagnostics.RemoveAll(ignored);

            return (diagnostics, output);
        }
    }
}
