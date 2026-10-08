using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stunts;

[assembly: Stunt<ISet<Guid>>]

var recording = new RecordingBehavior();
var builder = Stunt.Builder().AddBehavior(recording).AddBehavior(new DefaultValueBehavior());
var sample = builder.Build<ISample, IDisposable>();
Check(sample.Add(1, 2) == 0, "Interface interception");
sample.Split(out var number, out var names);
Check(number == 0 && names.Length == 0, "Output defaults");
Check(await sample.Load() == 0, "Task<int> defaults");
var tuple = await sample.Read();
Check(tuple.Item1 == 0 && tuple.Item2.Length == 0, "ValueTask tuple defaults");
Check(!sample.Items().Any(), "Enumerable defaults");
Check(sample.Grid().Length == 0, "Multidimensional array defaults");
Check(sample.Jagged().Length == 0, "Jagged array defaults");
try
{
    sample.Untyped();
    throw new InvalidOperationException("Untyped array defaults were silently accepted");
}
catch (ArgumentException error)
{
    Check(error.Message == "type", "Untyped array defaults preserve existing errors");
}
Check(sample.Optional() == null, "Nullable defaults");
Check(sample.Value().Value == 13, "Struct constructor defaults");
Check(sample.Location() == 0, "Ref-return defaults");
var input = 9;
Check(sample.Bump(ref input, out var output) == 0 && input == 9 && output == 0, "Mixed ref/output defaults");
Check(sample.Many(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17) == 0, "Long-signature metadata");
Check(Stunt.Builder().AddBehavior(recording).Build<ISample, IDisposable>().Default() == 7, "Default interface implementation");
((IDisposable)sample).Dispose();
Stunt.Get(sample).AddBehavior(recording);

var reference = Stunt.For<Greeter>();
reference.AddBehavior((invocation, next) => invocation.MethodBase.Name == nameof(Greeter.Name)
    ? invocation.CreateValueReturn("intercepted")
    : next(invocation, next));
var greeter = reference.ToObject();
Check(greeter.Seen == "intercepted", "Constructor interception");
var buffer = Stunt.Builder().AddBehavior(recording).Build<Buffer>();
#if NET9_0_OR_GREATER
Check(buffer.First(new[] { 9 }.AsSpan()) == 9, "Ref-struct interception");
#endif
CheckPointer(buffer);

var calculate = Stunt.For<Func<int, int>>(value => value + 1);
calculate.AddBehavior(recording);
Check(calculate.ToObject()(41) == 42, "Delegate binding");
Check(recording.Invocations.Count > 0, "Invocation metadata");

IDictionary<string, int> dictionary = builder.Build<IDictionary<string, int>>();
Check(dictionary.Count == 0, "Closed generic stunt");
Check(CreateDictionary<long, string>().Count == 0, "Generic wrapper closed at its call site");
Check(new CompiledStuntFactory().CreateStunt(typeof(ISample).Assembly, typeof(ISet<Guid>), Type.EmptyTypes, Array.Empty<object>()) is IStunt, "Assembly-registered stunt");

var defaults = new DefaultValueProvider();
defaults.Register<int>(() => 3);
var configured = Stunt.For<ISample, IDisposable>().AddBehavior(new DefaultValueBehavior(defaults)).ToObject();
Check(await configured.Load() == 3, "Custom typed defaults");
Check(defaults.Deregister(typeof(Task<>)) && configured.Load() == null, "Deregistered generic defaults");
Check(!defaults.Deregister(typeof(Task<>)), "Repeated deregistration");

var fallback = Stunt.For<ISample, IDisposable>().AddBehavior(new DefaultValueBehavior(new CustomDefaults())).ToObject();
Check(await fallback.Load() == 11, "Virtual default provider fallback");

var swallowed = Stunt.For<ISample, IDisposable>()
    .AddBehavior(new ExceptionMappingBehavior(static _ => null, swallow: true))
    .ToObject();
Check(swallowed.Value().Value == 13, "Swallowed struct uses the generated default");
Check(swallowed.Grid().Length == 0, "Swallowed array uses the generated default");
Check(await swallowed.Load() == 0, "Swallowed task uses the generated default");

var supplied = new DefaultValueProvider();
supplied.Register<int>(() => 4);
var mappedDefaults = Stunt.For<ISample, IDisposable>()
    .AddBehavior(new ExceptionMappingBehavior(static _ => null, swallow: true, supplied))
    .ToObject();
Check(mappedDefaults.Add(1, 2) == 4, "Swallowed call uses the supplied default provider");

try
{
    Stunt.For<ISample, IDisposable>()
        .AddBehavior(new ExceptionMappingBehavior(exception => new InvalidOperationException("mapped", exception)))
        .ToObject()
        .Add(1, 2);
    throw new InvalidOperationException("Mapped exception was swallowed");
}
catch (InvalidOperationException error) when (error.Message == "mapped")
{
    Check(error.InnerException is NotImplementedException, "Mapped exception keeps the original inner");
}

var noDefaults = new DefaultValueProvider(false);
Check(noDefaults.GetDefault<(int, string[])>().Item2 == null, "Disabled default factories");

try
{
    new CompiledStuntFactory().CreateStunt(typeof(ISample).Assembly, typeof(IDictionary<long, byte>), Type.EmptyTypes, Array.Empty<object>());
    throw new InvalidOperationException("An unregistered stunt was accepted");
}
catch (NotSupportedException error)
{
    Check(error.Message.Contains("[assembly: Stunt<System.Collections.Generic.IDictionary<long, byte>>]"), "Unregistered stunt explanation");
}
Console.WriteLine("Native AOT stunt scenarios passed.");

static void Check(bool condition, string scenario)
{
    if (!condition)
        throw new InvalidOperationException(scenario);
}

static unsafe void CheckPointer(Buffer buffer)
{
    var value = 9;
    Check(buffer.Read(&value) == 9, "Pointer interception");
}

[StuntGenerator]
static IDictionary<TKey, TValue> CreateDictionary<TKey, TValue>() where TKey : notnull
    => Stunt.Builder().AddBehavior(new DefaultValueBehavior()).Build<IDictionary<TKey, TValue>>();

public interface ISample
{
    int Add(int x, int y);
    void Split(out int number, out string[] names);
    Task<int> Load();
    ValueTask<(int, string[])> Read();
    IEnumerable<int> Items();
    int[,] Grid();
    int[][] Jagged();
    Array Untyped();
    int? Optional();
    SampleValue Value();
    ref int Location();
    int Bump(ref int input, out int output);
    int Many(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q);
    int Default() => 7;
}

public class Greeter
{
    public Greeter() => Seen = Name();
    public string Seen { get; }
    public virtual string Name() => "base";
}

public struct SampleValue
{
    public SampleValue() => Value = 13;
    public int Value { get; }
}

public class Buffer
{
#if NET9_0_OR_GREATER
    public virtual int First(Span<int> values) => values[0];
#endif
    public virtual unsafe int Read(int* value) => *value;
}

public class CustomDefaults : DefaultValueProvider
{
    protected override object? GetFallbackDefaultValue(Type type)
        => type == typeof(int) ? 11 : base.GetFallbackDefaultValue(type);
}
