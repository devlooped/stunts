using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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

    public abstract class ExternalMetadataBase
    {
        protected internal abstract int Value { get; }
        protected internal virtual void Invoke() { }
    }

    public interface IExternalInternalSetter
    {
        object Subject { get; internal set; }
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
        public void GeneratesSameNamedTypesFromDifferentNamespaces()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;

namespace First { public interface IItem { int Value => 1; } }
namespace Second { public interface IItem { int Value => 2; } }
public static class Test
{
    public static First.IItem First() => Stunt.Of<First.IItem>();
    public static Second.IItem Second() => Stunt.Of<Second.IItem>();
}");

            Assert.Empty(diagnostics);
            var assembly = compilation.Emit(false);
            Assert.NotNull(assembly.GetType("Stunts.First.IItemStunt"));
            Assert.NotNull(assembly.GetType("Stunts.Second.IItemStunt"));
        }

        [Fact]
        public void GeneratesTypesDifferingOnlyByCase()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;

public interface IItem { }
public interface Iitem { }
public static class Test
{
    public static IItem First() => Stunt.Of<IItem>();
    public static Iitem Second() => Stunt.Of<Iitem>();
}");

            Assert.Empty(diagnostics);
            var assembly = compilation.Emit(false);
            Assert.NotNull(assembly.GetType("Stunts.IItemStunt"));
            Assert.NotNull(assembly.GetType("Stunts.IitemStunt"));
        }

        [Fact]
        public void ReportsSealedTypeWithoutDiscardingOtherStunts()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using Stunts;

public sealed class SealedType { }
public static class Test
{
    public static object Invalid() => Stunt.Of<SealedType>();
    public static IDisposable Valid() => Stunt.Of<IDisposable>();
}");

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal(StuntDiagnostics.SealedBaseType.Id, diagnostic.Id);
            var name = new NamingConvention().GetFullName(new[]
            {
                compilation.GetTypeByMetadataName("System.IDisposable"),
            });
            Assert.NotNull(compilation.GetTypeByMetadataName(name));
        }

        [Fact]
        public void IgnoresNestedInterfaceTypes()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;

public interface IWithNestedTypes
{
    class NestedClass { }
    interface NestedInterface { }
    enum NestedEnum { Value }
    void Invoke();
}

public static class Test
{
    public static IWithNestedTypes Create() => Stunt.Of<IWithNestedTypes>();
}");

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void GeneratesMetadataSignatures()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                #nullable enable
                using System;
                using System.Threading;
                using Stunts;

                public interface IMetadata
                {
                    object Subject { get; internal set; }
                    void @event(string @class, CancellationToken token = default, string? text = null);
                    void Defaults(int? count = null, DayOfWeek day = DayOfWeek.Monday, short value = -1);
                    void Generic<T>(T value = default!);
                }
                public abstract class MetadataBase
                {
                    public abstract T? Value<T>(object key);
                    public abstract System.Collections.Generic.IEnumerable<T?> Values<T>();
                    public abstract void Invoke<T>(T value) where T : IDisposable, new();
                }
                public static class Test
                {
                    public static IMetadata Interface() => Stunt.Of<IMetadata>();
                    public static MetadataBase Class() => Stunt.Of<MetadataBase>();
                }
                """);

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void GeneratesExternalProtectedInternalOverrides()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using Stunts;
                using Stunts.UnitTests;
                public static class Test
                {
                    public static ExternalMetadataBase Create() => Stunt.Of<ExternalMetadataBase>();
                }
                """);

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void GeneratesBehaviorPropertyCollision()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using Stunts;
                public interface IWithBehaviors { object Behaviors { get; } }
                public static class Test
                {
                    public static IWithBehaviors Create() => Stunt.Of<IWithBehaviors>();
                }
                """);

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void ReportsInaccessibleSetterWithoutDiscardingOtherStunts()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using System;
                using Stunts;
                using Stunts.UnitTests;
                public static class Test
                {
                    public static IExternalInternalSetter Invalid() => Stunt.Of<IExternalInternalSetter>();
                    public static IDisposable Valid() => Stunt.Of<IDisposable>();
                }
                """);

            Assert.Equal(StuntDiagnostics.InaccessibleInterfaceMember.Id, Assert.Single(diagnostics).Id);
            Assert.Contains("IExternalInternalSetter.Subject.set", diagnostics[0].GetMessage());
            Assert.NotNull(compilation.GetTypeByMetadataName("Stunts.System.IDisposableStunt"));
            Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void GeneratesHiddenMembersAndNamedIndexers()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using System.Runtime.CompilerServices;
                using Stunts;
                public class HiddenBase { public virtual T Value<T>(object key) => default; }
                public class HiddenDerived : HiddenBase { public object Value { get; } }
                public abstract class Indexed
                {
                    public abstract object Item(int index);
                    [IndexerName("ItemOf")]
                    public virtual object this[int index] => Item(index);
                }
                public static class Test
                {
                    public static HiddenDerived Hidden() => Stunt.Of<HiddenDerived>();
                    public static Indexed Indexer() => Stunt.Of<Indexed>();
                }
                """);

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void GeneratesPointerProperties()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using Stunts;
                public unsafe interface IPointers { void** Table { get; set; } }
                public static class Test { public static IPointers Create() => Stunt.Of<IPointers>(); }
                """);

            Assert.Empty(diagnostics);
            compilation.Emit(false);
        }

        [Fact]
        public void ConstructorRefStructHoldersAreInvalidated()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using System;
                using Stunts;
                public class SpanConstructor
                {
                    public SpanConstructor(Span<int> data, out int value) => value = data[0];
                }
                public class Test : IBehaviorPipelineFactory, IStuntBehavior
                {
                    SpanRef<int> captured;
                    bool fail;
                    public static SpanConstructor Generate() => Stunt.Of<SpanConstructor>();
                    public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline(this);
                    public bool AppliesTo(IMethodInvocation invocation) => invocation.MethodBase.IsConstructor;
                    public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
                    {
                        captured = (SpanRef<int>)invocation.Arguments.GetValue("data");
                        if (captured.Value[0] != 7)
                            throw new Exception("Constructor argument was not preserved.");
                        if (fail)
                            throw new InvalidOperationException();
                        return next(invocation, next);
                    }
                    public static bool Run(bool fail)
                    {
                        var test = new Test { fail = fail };
                        using var ambient = BehaviorPipelineFactory.UseAmbient(test);
                        try
                        {
                            int value = 0;
                            SpanConstructor stunt = null;
                            _ = stunt;
                            if (fail || value != 7)
                                return false;
                        }
                        catch (InvalidOperationException) when (fail) { }
                        try
                        {
                            var value = test.captured.Value;
                            return false;
                        }
                        catch (AccessViolationException) { return true; }
                    }
                }
                """);

            Assert.Empty(diagnostics);
            var source = compilation.SyntaxTrees.Single(tree => tree.FilePath == nameof(ConstructorRefStructHoldersAreInvalidated) + ".cs");
            compilation = compilation.ReplaceSyntaxTree(source, CSharpSyntaxTree.ParseText(
                source.ToString().Replace("SpanConstructor stunt = null;",
                    "var stunt = new global::Stunts.SpanConstructorStunt(new[] { 7 }, out value);"),
                (CSharpParseOptions)source.Options, source.FilePath, Encoding.UTF8));
            var assembly = compilation.Emit(false);
            var run = assembly.GetType("Test")!.GetMethod("Run")!;
            Assert.Equal(true, run.Invoke(null, new object[] { false }));
            Assert.Equal(true, run.Invoke(null, new object[] { true }));
        }

        [Fact]
        public void GeneratesLongAndShadowingSignatures()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                #nullable enable
                using System;
                using Stunts;
                public abstract class Signatures
                {
                    public virtual int Shadow(int pipeline, int invocation, int next) => pipeline + invocation + next;
                    public abstract string? TryRead(out string? value);
                    public abstract void Many(int a, int b, int c, int d, int e, int f, int g, int h,
                        int i, int j, int k, int l, int m, int n, int o, int p, int q, int r);
                    public abstract void ManyOut(int a, int b, int c, int d, int e, int f, int g, int h,
                        int i, int j, int k, int l, int m, int n, int o, int p, int q, out int r);
                    public abstract void DelegateOut(ExecuteHandler implementation, out int value);
                    public virtual int Names(ref int method, ref int result, ref int invocation, ref int next)
                        => method + result + invocation + next;
                    public virtual int Hold(ReadOnlySpan<int> returned, out int returnedRef,
                        ref int value, ref int invocation, ref int next, ref int outputs, ref int result)
                    {
                        returnedRef = returned.Length;
                        return returned.Length;
                    }
                }
                public static class Test
                {
                    public static Signatures Create() => Stunt.Of<Signatures>();
                }
                """);

            Assert.Empty(diagnostics);
            var assembly = compilation.Emit(false);
            var instance = assembly.GetType("Test")!.GetMethod("Create")!.Invoke(null, null)!;
            Stunt.Get(instance).AddBehavior(new DefaultValueBehavior());
            var arguments = Enumerable.Repeat<object>(1, 18).ToArray();
            instance.GetType().GetMethod("ManyOut")!.Invoke(instance, arguments);
            Assert.Equal(0, arguments[17]);
        }

        [Theory]
        [InlineData("System.TypedReference")]
        [InlineData("System.ArgIterator")]
        [InlineData("System.RuntimeArgumentHandle")]
        public async Task ReportsUnsupportedRuntimeSignatures(string type)
        {
            var (diagnostics, compilation) = GetGeneratedOutput($$"""
                using Stunts;
                public interface IRuntimeSignature { void Read({{type}} value); }
                public static class Test
                {
                    public static IRuntimeSignature Invalid() => Stunt.Of<IRuntimeSignature>();
                }
                """);

            Assert.Equal(StuntDiagnostics.UnsupportedRuntimeSignature.Id, Assert.Single(diagnostics).Id);
            var analyzed = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ValidateTypesAnalyzer())).GetAnalyzerDiagnosticsAsync();
            Assert.Equal(StuntDiagnostics.UnsupportedRuntimeSignature.Id, Assert.Single(analyzed).Id);
            Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesConstructedGenericStuntsFromGenericMethod(bool annotateFactory)
        {
            var code = @"
using System;
using System.Collections.Generic;
using Stunts;

namespace UnitTests
{
    public static class Test
    {
        public static object[] Run()
            => new object[] { Create<string, int>(), Create<Guid, string>() };

        $ATTRIBUTE$
        static IDictionary<T0, T1> Create<T0, T1>()
            => global::Stunts.Stunt.Of<global::System.Collections.Generic.IDictionary<T0, T1>>();
    }
}".Replace("$ATTRIBUTE$", annotateFactory ? "[StuntGenerator]" : "");

            var (diagnostics, compilation) = GetGeneratedOutput(code, test: nameof(CreatesConstructedGenericStuntsFromGenericMethod) + annotateFactory);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Empty(await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ValidateTypesAnalyzer())).GetAnalyzerDiagnosticsAsync());

            var assembly = compilation.Emit(false);
            var instances = (object[])assembly.GetType("UnitTests.Test")!.GetMethod("Run")!.Invoke(null, null)!;
            var first = Assert.IsAssignableFrom<IDictionary<string, int>>(instances[0]);
            var second = Assert.IsAssignableFrom<IDictionary<Guid, string>>(instances[1]);

            Assert.IsAssignableFrom<IStunt>(first);
            Assert.IsAssignableFrom<IStunt>(second);
            Assert.Equal(first.GetType().GetGenericTypeDefinition(), second.GetType().GetGenericTypeDefinition());

            var unseen = new CompiledStuntFactory().CreateStunt(assembly,
                typeof(IDictionary<DateTime, decimal>), Array.Empty<Type>(), Array.Empty<object>());
            Assert.IsAssignableFrom<IDictionary<DateTime, decimal>>(unseen);
            Assert.Equal(first.GetType().GetGenericTypeDefinition(), unseen.GetType().GetGenericTypeDefinition());

            Stunt.Get(first).AddBehavior(new DefaultValueBehavior());
            Stunt.Get(second).AddBehavior(new DefaultValueBehavior());

            Assert.Equal(0, first["key"]);
            Assert.Null(second[Guid.Empty]);
        }

        [Fact]
        public void CreatesClosedGenericStuntFromAnnotatedTypeFactory()
        {
            var code = @"
using System.Collections.Generic;
using Stunts;

namespace UnitTests
{
    public static class Test
    {
        public static IDictionary<string, int> Run()
            => Create<IDictionary<string, int>>();

        [StuntGenerator]
        static T Create<T>() => Stunt.Of<T>();
    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            var assembly = compilation.Emit(false);
            var instance = assembly.GetType("UnitTests.Test")!.GetMethod("Run")!.Invoke(null, null);
            var dictionary = Assert.IsAssignableFrom<IDictionary<string, int>>(instance);

            Assert.IsAssignableFrom<IStunt>(dictionary);
            Stunt.Get(dictionary).AddBehavior(new DefaultValueBehavior());

            Assert.Equal(0, dictionary["key"]);
        }

        [Fact]
        public void AnnotatedTypeFactoryStillGeneratesCallerTypeWhenItAlsoUsesTemplates()
        {
            var code = @"
using System;
using System.Collections.Generic;
using Stunts;

namespace UnitTests
{
    public static class Test
    {
        public static IServiceProvider Run() => Create<IServiceProvider>();

        [StuntGenerator]
        static T Create<T>()
        {
            _ = Stunt.Of<ICollection<T>>();
            return Stunt.Of<T>();
        }

    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            var assembly = compilation.Emit(false);
            var instance = assembly.GetType("UnitTests.Test")!.GetMethod("Run")!.Invoke(null, null);

            Assert.IsAssignableFrom<IServiceProvider>(instance);
            Assert.IsAssignableFrom<IStunt>(instance);
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
                new ISourceGenerator[] { new StuntGenerator(), new SignatureRefGenerator().AsSourceGenerator() },
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
