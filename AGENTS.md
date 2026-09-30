# Stunts design notes

## Behavior configuration API

`AddBehavior`/`InsertBehavior` (in `src/Stunts/StuntExtensions.cs`) are generic extension
methods constrained to `IStunt`:

```csharp
public static TStunt AddBehavior<TStunt>(this TStunt stunt, ...) where TStunt : IStunt
```

The constraint makes the API discoverable only on actual stunts (instead of any object), and
the generic type parameter preserves the concrete receiver type so calls can be chained.

## `Stunt.Of` vs `Stunt.For` vs `Stunt.Get`

`src/Stunts.Package/Stunt.cs` ships as content into consuming projects and is the
`[StuntGenerator]`-annotated factory API:

| API | Returns | Use |
|-----|---------|-----|
| `Stunt.Of<T>(...)` | `T` | Just the stunt, no behavior configuration needed |
| `Stunt.For<T>(...)` | `StuntReference<T>` | Create and configure behaviors |
| `Stunt.Get<T>(T stunt)` | `StuntReference<T>` | Configure an already created stunt |
| `Stunt.Builder()` | `StuntBuilder` | Configure behaviors once, build many stunts |

`StuntReference<T>` implements `IStunt`, so the behavior extension methods apply to it
directly, and exposes the stunt via `ToObject()`.

`Stunt.For<T>` passes a `Lazy<T>`. The stunt is constructed on the first `ToObject()` call
(the implicit conversion calls `ToObject()` too). That call installs the behaviors configured
so far with `BehaviorPipelineFactory.UseAmbient`, so virtual members invoked from a base
constructor are intercepted. If none were added, the factory already current at that call is
used. After construction, `Behaviors` forwards to the stunt's own pipeline. `Stunt.Get<T>`
still wraps an already constructed instance and forwards `Behaviors` immediately.

An implicit conversion to `T` is also declared, but **C# does not allow user-defined
conversions to interface types**, so it only kicks in for class and delegate stunts. Since most
stunts are interfaces, `ToObject()` is the usage pattern tests and docs should show. This
limitation is why `Stunt.Of<T>` keeps returning `T` instead of the reference type.

Delegate stunts are unwrapped when the instance is materialized: for a delegate `T`, the
generated stunt is the delegate's `Target`.

The VB content file (`src/Stunts.Package/Stunt.vb`) mirrors the C# one: `StuntReference(Of T)`,
`StuntBuilder`, `Get`, and the `Of`/`For`/`Build` overloads (including the delegate ones), with two
VB-specific notes:

- VB cannot constrain a type parameter to `System.Delegate` (BC32061), so the delegate `Of`/`For`
  overloads are declared as `(Of T)(implementation As T)` without a constraint. Behavior is
  identical (the argument is passed as the single constructor argument), but a call passing a
  lone `Nothing` (e.g. `Stunt.Of(Of IFoo)(Nothing)`) is ambiguous: use
  `Stunt.Of(Of IFoo)(New Object() {Nothing})`. The overload is kept because it's what gives
  lambdas their parameter type inference in `Stunt.Of(Of MyDelegate)(Function(x, y) x + y)`.
- The `Widening` conversion to `T` has the same interface limitation as C# (BC30512 under
  `Option Strict On`), so `ToObject()` is the pattern to show.

There is no VB project in the solution, so changes to this file are validated by compiling it
in a scratch VB project referencing `src/Stunts/bin/Debug/netstandard2.0/Stunts.dll`.

Every factory overload (including the `For` and `Build` ones) must carry `[StuntGenerator]`: the source
generator keys off that attribute on the invoked method (instance or static) and uses the call site's
generic type arguments to decide which stunt types to generate.

## `StuntBuilder`

`StuntBuilder` (also in `Stunt.cs`/`Stunt.vb`) implements `IStunt`, so the `AddBehavior`/
`InsertBehavior` extension methods configure the list of behaviors *being built*, returning the
builder itself for chaining. Its `Build<T>` overloads mirror `Stunt.Of<T>` one to one (including
the delegate one and the `T1`..`T8` extra interfaces), but wrap the stunt creation in
`BehaviorPipelineFactory.UseAmbient` with a factory that seeds every new pipeline from the
builder's behaviors.

That's the key difference with `Stunt.Of`/`Stunt.For`: the behaviors are already in the pipeline
when the stunt constructor runs, so they can intercept virtual members invoked from base class
constructors (`ClassProxyTests.VirtualCallDuringConstructionUsesThePipelineFactory` shows the
raw ambient-factory version of the same thing).

`BehaviorPipeline`'s `IEnumerable<IStuntBehavior>` constructor copies the list, so each built
stunt gets a snapshot of the behaviors at build time, while sharing the behavior *instances*
(a single `RecordingBehavior` records all stunts from the builder).

The scenario at `src/Stunts.UnitTests/Scenarios/StuntBuilder.cs` covers all of the above through
the real source generator (it uses the namespace `Stunts.Scenarios.Builders` because a
`StuntBuilder` namespace segment would shadow the type).

## Test usage pattern

Tests create the reference, configure behaviors on it, and assign the stunt to an explicitly
typed local so the invoked type is obvious. Behaviors added before `ToObject()` are in place
during construction; behaviors added after still modify the live pipeline:

```csharp
var stunt = Stunt.For<ICalculator>();
ICalculator calculator = stunt.ToObject();

stunt.AddBehavior(new DefaultValueBehavior());

Assert.Equal(0, calculator.Add(1, 2));
```

Note that calling members on the reference itself would target `StuntReference<T>` (for
`ToString`, `GetHashCode` and `Equals`), not the stunt.

## Building locally

The `Stunts` and `Stunts.CodeAnalysis` assemblies are consumed as analyzers by other projects
in the solution, so a full `dotnet build` may fail with file locks (`CS2012`) when an IDE has
the solution open. Close the IDE, and if needed build projects one at a time with
`/p:UseSharedCompilation=false`. Tests run with `dnx --yes retest`.
