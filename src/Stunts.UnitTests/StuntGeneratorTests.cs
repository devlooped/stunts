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
        public BaseClassStunt() => pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (target, m) => m.CreateValueReturn(this, m.Arguments)));
        [CompilerGenerated]
        IList<IStuntBehavior> IStunt.Behaviors => pipeline.Behaviors;
        [CompilerGenerated]
        public override bool Equals(object obj) => pipeline.Execute<bool>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (target, m) => m.CreateValueReturn(base.Equals(obj), obj), obj));
        [CompilerGenerated]
        public override int GetHashCode() => pipeline.Execute<int>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (target, m) => m.CreateValueReturn(base.GetHashCode())));
        [CompilerGenerated]
        public override string ToString() => pipeline.Execute<string>(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), (target, m) => m.CreateValueReturn(base.ToString())));
        [CompilerGenerated]
        public override bool TryMixed(int x, int? y, ref string name, out int? z)
        {
            var _method = MethodBase.GetCurrentMethod();
            z = default;
            var _result = pipeline.Invoke(MethodInvocation.Create(this, _method, (target, m) =>
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
        [Fact]
        public void BindsExplicitGeneratorCallsAmongUnrelatedGenericCalls()
        {
            var noise = string.Join(Environment.NewLine, Enumerable.Range(0, 40).Select(index =>
                $"static int Noise{index}<T>(T value) => 0; static int Use{index}() => Noise{index}<int>(1) + items.Select<int, int>(x => x).Count();"));
            var (diagnostics, compilation) = GetGeneratedOutput($$"""
                using System;
                using System.Linq;
                using Stunts;
                public static class Test
                {
                    static int[] items = new int[0];
                    {{noise}}
                    public static IDisposable Create() => Stunt.Of<IDisposable>();
                }
                """);

            Assert.Empty(diagnostics);
            Assert.Contains(compilation.SyntaxTrees, tree => tree.ToString().Contains("CompiledStuntFactory.Register("));
        }

        [Fact]
        public void RegistersClosedStunts()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using Stunts;
public static class Test { public static IDisposable Create() => Stunt.Of<IDisposable>(); }");

            Assert.Empty(diagnostics);
            Assert.Contains(compilation.SyntaxTrees, tree => tree.ToString().Contains("CompiledStuntFactory.Register("));
            var assembly = compilation.Emit(false);
            Assert.IsAssignableFrom<IStunt>(new CompiledStuntFactory().CreateStunt(
                assembly, typeof(IDisposable), Array.Empty<Type>(), Array.Empty<object>()));
        }

        [Fact]
        public void RegistersClosedAsyncAdapters()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System.Threading.Tasks;
using Stunts;
public interface IService
{
    Task<int> GetAsync();
    ValueTask<string> ReadAsync();
    Task RunAsync();
}
public static class Test { public static IService Create() => Stunt.Of<IService>(); }");

            Assert.Empty(diagnostics);
            var text = string.Concat(compilation.SyntaxTrees.Select(tree => tree.ToString()));
            Assert.Contains("AsyncRegistry.Register<int>()", text);
            Assert.Contains("AsyncRegistry.Register<string>()", text);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "AsyncRegistry.Register<").Count);
        }

        [Fact]
        public void SkipsUncallableConstructorsInRegistration()
        {
            var (diagnostics, compilation) = GetGeneratedOutput("""
                using System;
                using System.Diagnostics.CodeAnalysis;
                using Stunts;

                public class ObsoleteCtor
                {
                    [Obsolete("retired", true)]
                    public ObsoleteCtor(object request) { }
                    public ObsoleteCtor() { }
                    public virtual int Value() => 1;
                }

                public class NeedsInitializer
                {
                    public NeedsInitializer() { }
                    public required string Name { get; set; }
                    public virtual int Value() => 1;
                }

                public class SetsRequired
                {
                    [SetsRequiredMembers]
                    public SetsRequired() { }
                    public required int Count { get; set; }
                    public virtual int Value() => 1;
                }

                public static class Test
                {
                    public static ObsoleteCtor CreateObsolete() => Stunt.Of<ObsoleteCtor>();
                    public static NeedsInitializer CreateNeeds() => Stunt.Of<NeedsInitializer>();
                    public static SetsRequired CreateSets() => Stunt.Of<SetsRequired>();
                }
                """);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            var registration = string.Join("\n", compilation.SyntaxTrees.Select(tree => tree.ToString()).Where(text => text.Contains("class StuntRegistrations")));
            Assert.Contains("ObsoleteCtorStunt()", registration);
            Assert.DoesNotContain("ObsoleteCtorStunt((object)args[0])", registration);
            Assert.DoesNotContain("NeedsInitializerStunt", registration);
            Assert.Contains("SetsRequiredStunt()", registration);
            Assert.Contains("SetsRequiredMembersAttribute", string.Join("\n", compilation.SyntaxTrees.Select(tree => tree.ToString())));
        }

        [Fact]
        public void UnregisteredStuntReportsAttributeToAdd()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using Stunts;
public static class Test { public static IDisposable Create() => Stunt.Of<IDisposable>(); }");

            Assert.Empty(diagnostics);
            var assembly = compilation.Emit(false);
            var error = Assert.Throws<NotSupportedException>(() => new CompiledStuntFactory().CreateStunt(
                assembly, typeof(IDictionary<DateTime, decimal>), new[] { typeof(IDisposable) }, Array.Empty<object>()));

            Assert.Contains("[assembly: Stunt<System.Collections.Generic.IDictionary<System.DateTime, decimal>, System.IDisposable>]", error.Message);
            Assert.Contains(assembly.GetName().Name!, error.Message);
        }

        [Fact]
        public void RegistersAssemblyAttributeStunts()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using System.Collections.Generic;
using Stunts;
[assembly: Stunt<IDictionary<string, int>, IDisposable>]
[assembly: Stunt<IList<Guid>>]
public static class Test { }");

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            var assembly = compilation.Emit(false);
            var factory = new CompiledStuntFactory();
            var dictionary = factory.CreateStunt(assembly, typeof(IDictionary<string, int>), new[] { typeof(IDisposable) }, Array.Empty<object>());
            Assert.IsAssignableFrom<IDisposable>(dictionary);
            Assert.IsAssignableFrom<IList<Guid>>(factory.CreateStunt(assembly, typeof(IList<Guid>), Array.Empty<Type>(), Array.Empty<object>()));
        }

        [Fact]
        public void ReportsAmbiguousConstructorArguments()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;
public class Ambiguous
{
    public Ambiguous(string value) { }
    public Ambiguous(System.Uri value) { }
    public virtual int Value() => 1;
}
public static class Test
{
    public static Ambiguous Null() => Stunt.Of<Ambiguous>(new object[] { null });
    public static Ambiguous Text() => Stunt.Of<Ambiguous>(""text"");
}");

            Assert.Empty(diagnostics);
            var assembly = compilation.Emit(false);
            var test = assembly.GetType("Test")!;
            Assert.IsAssignableFrom<IStunt>(test.GetMethod("Text")!.Invoke(null, null));
            var error = Assert.Throws<TargetInvocationException>(() => test.GetMethod("Null")!.Invoke(null, null));
            Assert.IsType<AmbiguousMatchException>(error.InnerException);
        }

        [Fact]
        public async Task ReportsUnannotatedGenericWrappers()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System.Collections.Generic;
using Stunts;
public static class Test
{
    public static object Run() => Create<string, int>();
    public static IDictionary<TKey, TValue> Create<TKey, TValue>() => Stunt.Of<IDictionary<TKey, TValue>>();
}");

            Assert.Empty(diagnostics);
            var analyzed = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GenericWrapperAnalyzer()))
                .GetAnalyzerDiagnosticsAsync();
            Assert.Equal(StuntDiagnostics.UnannotatedGenericWrapper.Id, Assert.Single(analyzed).Id);
        }

        [Fact]
        public void ReportsLanguageVersionWithoutModuleInitializers()
        {
            var (diagnostics, _) = GetGeneratedOutput(@"
using System;
using Stunts;
public static class Test { public static IDisposable Create() => Stunt.Of<IDisposable>(); }", languageVersion: LanguageVersion.CSharp8);

            Assert.Equal(StuntDiagnostics.LanguageVersionNotSupported.Id, Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void ReportsNestedStuntsInGenericTypes()
        {
            var (diagnostics, _) = GetGeneratedOutput(@"
using Stunts;
public partial class Outer<T>
{
    protected class Hidden { public virtual int Value() => 1; }
}
public partial class Derived : Outer<int>
{
    public static object Create() => Stunt.Of<Hidden>();
}");

            Assert.Equal(StuntDiagnostics.GenericContainingType.Id, Assert.Single(diagnostics).Id);
        }

        [Fact]
        public void WarnsAboutGenericInterceptedMethodsForAot()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;
public interface IGeneric { T Echo<T>(T value); }
public static class Test { public static IGeneric Create() => Stunt.Of<IGeneric>(); }", aotProperty: "PublishAot");

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("ST015", diagnostic.Id);
            Assert.Contains("MakeGenericMethod", diagnostic.GetMessage());
            compilation.Emit(false);
        }

        [Fact]
        public void WarnsAboutQueryableDefaultsForAot()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System.Linq;
using Stunts;
public interface IQuery { IQueryable<int> Query(); }
public static class Test { public static IQuery Create() => Stunt.Of<IQuery>(); }", aotProperty: "PublishAot");

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("ST015", diagnostic.Id);
            Assert.Contains("DefaultValueProvider", diagnostic.GetMessage());
            compilation.Emit(false);
        }

        [Fact]
        public void CreatesInaccessibleNestedStunts()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using Stunts;
public partial class Outer
{
    class Hidden { public virtual int Value() => 1; }
    public static object Create() => Stunt.Of<Hidden>();
}", aotProperty: "PublishAot");

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            var instance = compilation.Emit(false).GetType("Outer")!.GetMethod("Create")!.Invoke(null, null);
            Assert.IsAssignableFrom<IStunt>(instance);
        }

        [Fact]
        public void ClosedFactoriesDoNotWarnForAot()
        {
            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using System.Collections.Generic;
using Stunts;
public static class Test
{
    public static IDictionary<string, int> Create() => Stunt.Of<IDictionary<string, int>>();
    public static IDisposable Reference() => Stunt.For<IDisposable>().ToObject();
    public static Func<int, int> Delegate() => Stunt.Builder().Build<Func<int, int>>(value => value);
}", aotProperty: "PublishAot");

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            var test = compilation.Emit(false).GetType("Test")!;
            Assert.IsAssignableFrom<IStunt>(test.GetMethod("Create")!.Invoke(null, null));
            Assert.IsAssignableFrom<IStunt>(test.GetMethod("Reference")!.Invoke(null, null));
            var function = Assert.IsType<Func<int, int>>(test.GetMethod("Delegate")!.Invoke(null, null));
            Assert.IsAssignableFrom<IStunt>(function.Target);
        }

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

        [Fact]
        public async Task CreatesConstructedGenericStuntsFromGenericMethod()
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

        [StuntGenerator]
        static IDictionary<T0, T1> Create<T0, T1>()
            => global::Stunts.Stunt.Of<global::System.Collections.Generic.IDictionary<T0, T1>>();
    }
}";

            var (diagnostics, compilation) = GetGeneratedOutput(code);

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Empty(await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ValidateTypesAnalyzer(), new GenericWrapperAnalyzer())).GetAnalyzerDiagnosticsAsync());

            var assembly = compilation.Emit(false);
            var instances = (object[])assembly.GetType("UnitTests.Test")!.GetMethod("Run")!.Invoke(null, null)!;
            var first = Assert.IsAssignableFrom<IDictionary<string, int>>(instances[0]);
            var second = Assert.IsAssignableFrom<IDictionary<Guid, string>>(instances[1]);

            Assert.IsAssignableFrom<IStunt>(first);
            Assert.IsAssignableFrom<IStunt>(second);
            Assert.False(first.GetType().IsGenericType);

            var error = Assert.Throws<NotSupportedException>(() => new CompiledStuntFactory().CreateStunt(assembly,
                typeof(IDictionary<DateTime, decimal>), Array.Empty<Type>(), Array.Empty<object>()));
            Assert.Contains("[assembly: Stunt<System.Collections.Generic.IDictionary<System.DateTime, decimal>>]", error.Message);

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
        public void AnnotatedTypeFactoryGeneratesAllClosedDefinitions()
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
            Assert.IsAssignableFrom<IStunt>(new CompiledStuntFactory().CreateStunt(assembly,
                typeof(ICollection<IServiceProvider>), Array.Empty<Type>(), Array.Empty<object>()));
        }

        [Fact]
        public void ClosesGenericWrappersFromReferencedAssemblies()
        {
            var (libraryDiagnostics, library) = GetGeneratedOutput(@"
using System.Collections.Generic;
using Stunts;

namespace Library
{
    public static class Factory
    {
        [StuntGenerator]
        public static IDictionary<string, T> Make<T>() => Inner<T>();

        [StuntGenerator]
        static IDictionary<string, T> Inner<T>() => Stunt.Of<IDictionary<string, T>>();
    }
}", test: "WrapperLibrary");

            Assert.Empty(libraryDiagnostics);
            Assert.Empty(library.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            using var stream = new MemoryStream();
            library.Emit(stream).AssertSuccess();
            var image = stream.ToArray();
            var libraryAssembly = Assembly.Load(image);

            var definitions = libraryAssembly.GetType("Stunts.Generated.StuntDefinitions");
            Assert.NotNull(definitions);
            var definition = Assert.Single(definitions!.GetMethods(BindingFlags.Public | BindingFlags.Static),
                method => method.GetCustomAttribute<StuntDefinitionAttribute>() != null);
            Assert.Equal(typeof(IDictionary<,>), definition.GetParameters().Single().ParameterType.GetGenericTypeDefinition());

            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using System.Collections.Generic;
using Library;

public static class Test
{
    public static object Run() => Factory.Make<int>();
}", test: "WrapperConsumer", references: new[] { MetadataReference.CreateFromImage(image) });

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            ResolveEventHandler resolve = (_, args) => new AssemblyName(args.Name).Name == "WrapperLibrary" ? libraryAssembly : null;
            AppDomain.CurrentDomain.AssemblyResolve += resolve;
            try
            {
                var assembly = compilation.Emit(false);
                var instance = assembly.GetType("Test")!.GetMethod("Run")!.Invoke(null, null);
                var dictionary = Assert.IsAssignableFrom<IDictionary<string, int>>(instance);
                Assert.IsAssignableFrom<IStunt>(dictionary);
                Assert.Same(assembly, dictionary.GetType().Assembly);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolve;
            }
        }

        [Fact]
        public void ClosesInferredIdentityWrapperFromReferencedAssembly()
        {
            var (libraryDiagnostics, library) = GetGeneratedOutput(@"
using Stunts;

namespace Library
{
    public static class Factory
    {
        [StuntGenerator]
        public static T Make<T>(T prototype) => Stunt.Of<T>();
    }
}", test: "IdentityLibrary");

            Assert.Empty(libraryDiagnostics);
            Assert.Empty(library.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            using var stream = new MemoryStream();
            library.Emit(stream).AssertSuccess();
            var image = stream.ToArray();
            var libraryAssembly = Assembly.Load(image);

            // The forward is implied, so the library records no stunt definition for it.
            Assert.Null(libraryAssembly.GetType("Stunts.Generated.StuntDefinitions"));

            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System;
using Library;

public static class Test
{
    public static object Run() => Factory.Make(default(IDisposable));
}", test: "IdentityConsumer", references: new[] { MetadataReference.CreateFromImage(image) });

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            ResolveEventHandler resolve = (_, args) => new AssemblyName(args.Name).Name == "IdentityLibrary" ? libraryAssembly : null;
            AppDomain.CurrentDomain.AssemblyResolve += resolve;
            try
            {
                var assembly = compilation.Emit(false);
                var instance = assembly.GetType("Test")!.GetMethod("Run")!.Invoke(null, null);
                Assert.IsAssignableFrom<IDisposable>(instance);
                Assert.IsAssignableFrom<IStunt>(instance);
                Assert.Same(assembly, instance.GetType().Assembly);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolve;
            }
        }

        [Fact]
        public void ClosesReferencedWrapperWhenConstraintTypeIsInaccessible()
        {
            var (libraryDiagnostics, library) = GetGeneratedOutput(@"
using System.Collections.Generic;
using Stunts;

namespace Library
{
    public class Factory
    {
        protected class Secret { }

        [StuntGenerator]
        protected static IList<T> Make<T, U>() where T : class where U : Secret => Stunt.Of<IList<T>>();
    }
}", test: "ConstraintLibrary");

            Assert.Empty(libraryDiagnostics);
            Assert.Empty(library.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            using var stream = new MemoryStream();
            library.Emit(stream).AssertSuccess();
            var image = stream.ToArray();
            var libraryAssembly = Assembly.Load(image);

            var definitions = libraryAssembly.GetType("Stunts.Generated.StuntDefinitions");
            Assert.NotNull(definitions);
            var definition = Assert.Single(definitions!.GetMethods(BindingFlags.Public | BindingFlags.Static),
                method => method.GetCustomAttribute<StuntDefinitionAttribute>() != null);
            Assert.Equal(typeof(IList<>), definition.GetParameters().Single().ParameterType.GetGenericTypeDefinition());
            var parameters = definition.GetGenericArguments();
            Assert.Equal(GenericParameterAttributes.ReferenceTypeConstraint, parameters[0].GenericParameterAttributes);
            Assert.Equal(GenericParameterAttributes.None, parameters[1].GenericParameterAttributes);
            Assert.Empty(parameters[0].GetGenericParameterConstraints());
            Assert.Empty(parameters[1].GetGenericParameterConstraints());

            var (diagnostics, compilation) = GetGeneratedOutput(@"
using System.Collections.Generic;
using Library;

public class Derived : Factory
{
    protected class MySecret : Secret { }

    public static object Run() => Make<string, MySecret>();
}", test: "ConstraintConsumer", references: new[] { MetadataReference.CreateFromImage(image) });

            Assert.Empty(diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

            ResolveEventHandler resolve = (_, args) => new AssemblyName(args.Name).Name == "ConstraintLibrary" ? libraryAssembly : null;
            AppDomain.CurrentDomain.AssemblyResolve += resolve;
            try
            {
                var assembly = compilation.Emit(false);
                var instance = assembly.GetType("Derived")!.GetMethod("Run")!.Invoke(null, null);
                var list = Assert.IsAssignableFrom<IList<string>>(instance);
                Assert.IsAssignableFrom<IStunt>(list);
                Assert.Same(assembly, list.GetType().Assembly);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolve;
            }
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

        static (ImmutableArray<Diagnostic>, Compilation) GetGeneratedOutput(string source, string[] additionalSources = null, [CallerMemberName] string? test = null, string aotProperty = null, bool aotEnabled = true,
            LanguageVersion languageVersion = LanguageVersion.Latest, MetadataReference[] references = null)
        {
            var libs = new HashSet<string>(File.ReadAllLines("lib.txt"), StringComparer.OrdinalIgnoreCase)
                .Distinct(FileNameEqualityComparer.Default)
                .ToDictionary(x => Path.GetFileName(x));

            var args = CSharpCommandLineParser.Default.Parse(
                File.ReadAllLines("csc.txt"), ThisAssembly.Project.MSBuildProjectDirectory, sdkDirectory: null);

            // net10 csc passes /features:InterceptorsNamespaces. The generator builds its
            // trees with default parse options, and Roslyn refuses to mix those features.
            var parseOptions = args.ParseOptions
                .WithLanguageVersion(languageVersion)
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

            var metadata = args.MetadataReferences.Select(x => libs.TryGetValue(Path.GetFileName(x.Reference), out var lib) ?
                    MetadataReference.CreateFromFile(lib) :
                    MetadataReference.CreateFromFile(x.Reference))
                .Concat(references ?? Array.Empty<MetadataReference>())
                .ToList();

            // Types passed to GenerateCode (e.g. BaseClass) live in this assembly.
            var testAssembly = typeof(StuntGeneratorTests).Assembly.Location;
            if (!string.IsNullOrEmpty(testAssembly) &&
                !metadata.Any(r => string.Equals(r.Display, testAssembly, StringComparison.OrdinalIgnoreCase)))
            {
                metadata.Add(MetadataReference.CreateFromFile(testAssembly));
            }

            var compilation = CSharpCompilation.Create(
                test,
                sources,
                metadata,
                args.CompilationOptions.WithCryptoKeyFile(null).WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

            Predicate<Diagnostic> ignored = d =>
                d.Severity == DiagnosticSeverity.Hidden ||
                d.Severity == DiagnosticSeverity.Info;

            var diagnostics = compilation.GetDiagnostics().RemoveAll(ignored);
            if (diagnostics.Any())
                return (diagnostics, compilation);

            var optionsProvider = EditorConfigOptionsProvider.Create(Directory.EnumerateFiles(
                Path.Combine(ThisAssembly.Project.MSBuildProjectDirectory, ThisAssembly.Project.IntermediateOutputPath),
                "*.editorconfig", SearchOption.TopDirectoryOnly));
            if (aotProperty != null)
                optionsProvider = new AotOptionsProvider(optionsProvider, "build_property." + aotProperty, aotEnabled);
            var driver = CSharpGeneratorDriver.Create(
                new ISourceGenerator[] { new StuntGenerator(), new SignatureRefGenerator().AsSourceGenerator() },
                parseOptions: parseOptions,
                optionsProvider: optionsProvider);

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out diagnostics);
            diagnostics = diagnostics.RemoveAll(ignored);

            return (diagnostics, output);
        }

        sealed class AotOptionsProvider : AnalyzerConfigOptionsProvider
        {
            readonly AnalyzerConfigOptionsProvider original;
            readonly AnalyzerConfigOptions options;

            public AotOptionsProvider(AnalyzerConfigOptionsProvider original, string property, bool enabled)
                => (this.original, options) = (original, new AotOptions(original.GlobalOptions, property, enabled));

            public override AnalyzerConfigOptions GlobalOptions => options;
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => original.GetOptions(tree);
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => original.GetOptions(textFile);

            sealed class AotOptions : AnalyzerConfigOptions
            {
                readonly AnalyzerConfigOptions original;
                readonly string property;
                readonly bool enabled;

                public AotOptions(AnalyzerConfigOptions original, string property, bool enabled)
                    => (this.original, this.property, this.enabled) = (original, property, enabled);

                public override bool TryGetValue(string key, out string value)
                {
                    if (key == property)
                    {
                        value = enabled ? "true" : "false";
                        return true;
                    }
                    return original.TryGetValue(key, out value);
                }
            }
        }
    }
}
