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

### Generic wrappers

Every generated stunt is a closed, non-generic type: there are no open generic templates.
A generic method passing its own type parameters to a generator method (e.g.
`static IDictionary<string, T> Make<T>() => Stunt.Of<IDictionary<string, T>>()`) is a *wrapper*
and must itself carry `[StuntGenerator]` (`ST016`, with a code fix adding the attribute).
`StuntClosure` (shared by analyzers and the generator) computes each wrapper's *stunt definitions*: the
stunt types it creates expressed over its own type parameters, flattened through nested wrappers.
Each concrete call site (`Make<int>()`) substitutes its type arguments into the definitions, so only
the combinations actually used are generated. Wrappers cannot use type parameters of a containing
type (`ST017`), be virtual/abstract/interface members (`ST018`), recurse with different type
arguments or chain more than `StuntClosure.MaxDepth` (8) wrappers (`ST019`). Local functions are
supported; their symbols are normalized to the closure's own semantic model, since local function
symbols bound by different models are not equal.

Cross-assembly: each stunt definition records the assembly that calls the leaf `Stunt.Of<T>` at run time
(the internal `Stunt` content class passes `typeof(Stunt).Assembly`). Libraries emit a public
`Stunts.Generated.StuntDefinitions` class whose `[StuntDefinition(docId, assembly)]` methods carry the
stunt definitions of externally visible wrappers as parameter types (trivial forwarding definitions are omitted).
Consumers read them from metadata, generate the closed stunts, and register them under the leaf
assembly's name.

Combinations no call site reveals (e.g. a runtime `Type`) are registered explicitly with
`[assembly: Stunt<T, T1..T8>]` (`StuntAttribute<...>` in `Stunts.dll`). The generator's
registration attribute is configurable alongside the generator attribute
(`StuntGenerator.WithRegistrationAttribute`).

`StuntGeneratorTests.CreatesConstructedGenericStuntsFromGenericMethod` and
`ClosesGenericWrappersFromReferencedAssemblies` cover single and cross-assembly closure;
`CodeAnalysis/ST016_UnannotatedGenericWrapper.cs` covers ST016-ST019 and the code fix;
`Scenarios/GenericWrappers.cs` covers nested and repeated arguments, constructors, builders,
delegates, default interface members, records, constraints, arrays, chains, local functions,
and assembly registrations through the real generator.

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

## Data annotations

`DataAnnotationsBehavior` (`src/Stunts.Extensions`) rejects a call when a built-in
`System.ComponentModel.DataAnnotations` validation attribute fails. The checks, the registry,
and the source generator come from `NativeValidation`. Property
attributes run on the setter. Method and constructor attributes run on the annotated parameters.
Getters are not checked. The first failure is a `ValidationException`, and the rest of the
pipeline does not run.

`StuntGeneratorAttribute` derives from `ValidationAttribute`, so the generator treats a generic
`[StuntGenerator]` method as a validated factory and registers the closed types that call names.
A factory that is not a stunt generator uses
`Devlooped.DataAnnotations.ValidatedAttribute`. `[assembly: Validate<T>]` registers a closed
type no factory call names. `Stunts.Extensions.targets` adds `$(NativeValidatorAnalyzer)` for consumers,
because the annotated types live in the consuming compilation.

`CompareAttribute` reads the other property from the instance, which re-enters the pipeline.
`Scenarios/DataAnnotationsBehavior.cs` covers the behavior through the package generator.

## Guards

`GuardBehavior` (`src/Stunts.Extensions`) takes `Func<IMethodInvocation, Exception?>`. The predicate
does not receive `next`. Null calls `next`. Any other result fails the invocation, and `next`
is not called.

A synchronous member returns that exception through `CreateExceptionReturn`, so the stunt throws
it on the caller thread. A `Task` or `ValueTask` member returns a faulted awaitable of the same
type before the target runs, including when the predicate returns `OperationCanceledException`.
The fault is built by `AsyncRegistry` from the adapters the source generator registers, which
keeps the behavior valid for Native AOT. The constructor is an invocation too, and a behavior
added before `ToObject()` sees it. `GuardBehaviorTests` covers the pipeline return shape.
`Scenarios/GuardBehavior.cs` covers the caller through the real generator.

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

## Generator execution

### Native AOT

Instantiation never goes through reflection: the generator emits a
`[ModuleInitializer]` `Stunts.Generated.StuntRegistrations.Initialize()` that calls
`CompiledStuntFactory.Register(requester, typeof(TStunt), types, new StuntConstructor(parameterTypes, args => new TStunt(...)))`
for every generated stunt (requires C# 9, `ST020`; a `ModuleInitializerAttribute` polyfill is
emitted when missing). Delegate stunts register `new TDelegate(new TStunt(...).Invoke)`. A params
array constructor also registers the form without the array, unless another constructor has that
signature. Inaccessible nested stunts get `__RegisterStunt{n}` forwarders injected into their
containers; they cannot be nested in generic types (`ST021`). `Register`'s `stuntType` preserves all
members, which roots invocation metadata for trimming.

`CompiledStuntFactory` keeps an order-insensitive base+interfaces registry per requesting
assembly (and by assembly name for cross-assembly registrations). On a miss it runs the
requester's module constructor once and retries, then throws `NotSupportedException` with the
exact `[assembly: Stunt<...>]` line to add (formatted by `CSharpTypeName`). No matching or
multiple matching constructors throw `MissingMethodException`/`AmbiguousMatchException`.
`IStuntFactory.CreateStunt` intentionally stays `Type`-based: downstream libraries (e.g.
Moq-like recursive mocks) create stunts from runtime `Type`s.

`CSharpAot` runs after pipeline rewriting. It roots closed invocation metadata, annotates the
exact `GetCurrentMethod` callers, and emits typed default factories in a static constructor.
Factory selection must preserve user registrations, deregistration, and virtual provider
fallbacks. Static constructors do not count as available instance constructors during generation.

`PublishAot`, `IsAotCompatible`, and `EnableAotAnalyzer` are compiler-visible properties.
`ST014` (from `Stunts.targets`) warns when those are set without compile-time stunts; `ST015`
warns about generic intercepted methods and queryable defaults. Ref-struct holders are emitted only
when the target runtime supports by-ref-like generics; Native AOT coverage includes .NET 8 and .NET 10.
Keep the netstandard2.0 runtime for analyzer hosts and legacy consumers, and ship the net8.0
runtime for Native AOT consumers.

Visual Basic only supports run-time stunts: `Stunts.targets` forces `EnableCompileTimeStunts`
to false for VB and warns (`ST011`) unless Stunts.DynamicProxy is installed. `GenericWrapperAnalyzer`
is C#-only and is skipped when `EnableCompileTimeStunts` is explicitly false.

`src/Acceptance/CompiledAot` is published and executed as a native binary in CI, for `net8.0`
and `net10.0`. It references the packed `Stunts` package, same as the other projects in
`Acceptance.slnx`, and covers generic wrappers, assembly registrations and the unregistered-stunt
message. It stays out of the package-building solution. On Windows, initialize the Visual C++
environment (with `vswhere.exe` on `PATH`) before publishing.

`DependencyResolver` accepts simple-name assembly probes (their `Version` is null),
and keeps registered paths in an immutable snapshot for concurrent compiler runs.
Assembly-load errors propagate to Roslyn instead of invoking `Debug.Fail`, which
can terminate the compiler process during diagnostic-resource probing.

Generator caches belong to a single execution: reuse semantic models per syntax
tree and inspect each factory's original definition once. Event rewriting binds
only declarations containing events. Interface scaffolding considers methods,
properties, and events, not nested type declarations.

Deduplicate stunts and default-interface helpers by run-time identity
(`NamingConvention.GetIdentity`, which ignores order, nullable annotations and tuple
element names). Names drop the namespaces and nesting of type arguments, so different
types can share a namespace-qualified name (`IFoo<A.Bar>`/`IFoo<B.Bar>`,
`IFoo<Wrap<int>, string>`/`IFoo<Wrap<int, string>>`): all of them then use
`GetUniqueName`/`GetUniqueDefaultImplementationName`, which append a stable hash, while
non-colliding names stay unchanged. Default helpers are named during parallel processing,
so their collisions are computed upfront (`ProcessorContext.AmbiguousDefaultImplementations`).
Array arguments are named `ArrayOfInt32`/`Array2DOfInt32`, in `StuntNaming` too.
Roslyn compares source hint names case-insensitively; qualify them and disambiguate
case-only collisions without changing generated type names. Sealed targets report
`ST003` while other candidates continue generating.

### Generation cache

`StuntCache` reuses the emitted text of a stunt while the types it implements keep their shape,
across builds and across projects. `Stunts.targets` (`ResolveStuntsCacheDir`, before
`GenerateMSBuildEditorConfigFileShouldRun`) sets the compiler-visible `StuntsCacheDir` to
`.vs/stunts-cache/` at the repository root, then the solution directory, then `obj/stunts-cache/`.
The root is the git `SourceRoot` without `NestedRoot` from `InitializeSourceControlInformation`
(as in ThisAssembly.Git): the repository containing the project, so worktrees and submodules get
their own.
`EnableStuntsCache=false` clears it. Without the property (unit tests, other hosts) nothing is
cached. Entries live in a folder per generator version (`StuntFingerprint.Version`: the MVIDs of
the generator, Roslyn, and processor assemblies), one content-addressed `.stunt` file per key,
written to a temporary file and renamed so concurrent builds never read partial entries. A
bounded in-memory copy, keyed by path, serves later compilations in the same compiler server.
Pruning runs at most daily: entries not read for 30 days, other versions' folders not written
for 7.

`StuntFingerprint` keys hash the compilation-wide inputs processors read (generator
configuration, the `DefaultImports` namespaces, the host culture, language version, nullable,
unsafe and metadata import options, pretty-printing, by-ref-like generics support, the corlib
identity, and the accessibility of the optional attributes the generated code uses, looked up
in the compilation the stunt binds against: the treeless one for metadata-only stunts), plus
each candidate type's declaration: attributes, containers, base types, interfaces, and every
member signature, including inherited ones and overridden members. Types used by those
signatures contribute their identity, kind, flags, accessibility, bases, interfaces, and
delegate signature, but not their members; attribute classes also contribute the
`AttributeUsage` of their base chain and the accessibility of named arguments' members.
Stunt keys also include the stunt's full name and the colliding default implementation names its
interfaces use, since collisions with the other stunts of a generation change them; default
implementation keys include their class name.
Processors from other assemblies disable the cache, since their inputs are unknown. Assemblies contribute only their
relation to the compilation (`self`, `friend` through `InternalsVisibleTo`, or `other`), so a
rebuilt or re-versioned library keeps its entries and test heads share them. Anything a
processor starts reading from the compilation must be added to the fingerprint, or hits
will emit stale code.

An entry stores the text added for the stunt (pretty-printed when `EmitCompilerGeneratedFiles`
is on), the flags that replay ST006/ST015 and the unchanged-scaffold skip, and the names of the
interfaces whose default implementation classes it calls; those classes are cached under their
own keys. Registration still runs every time: delegate and nested stunts are parsed back from
the cached text so their forwarders bind. `StuntCacheTests` covers hits, signature changes,
cross-project sharing, friend assemblies, nested and delegate stunts, default implementations,
diagnostics replay, name collisions, culture changes and custom processors; `STUNTS_PROFILE` reports hits, misses and the cache time.

### Signature compatibility

`MemberScaffold` preserves optional defaults, escaped identifiers, indexer metadata names,
and inherited override constraints. External `protected internal` overrides become
`protected`; constructor signatures exposing protected nested types remain protected.
Attribute replication must not introduce inaccessible type references or invalid constants.

Generated pipeline access is qualified with `this`, and generated locals/lambda parameters
avoid names from the intercepted signature. Signatures longer than the `MethodInvocation.Create`
overload limit use an `ArgumentCollection` instead. Ref-struct and pointer accessors use the same
holder path as methods. Ref-struct holders are invalidated in `finally`, including constructor
interception; constructor output arguments already initialized by the base call must be preserved.
Mixed holder and ordinary `ref`/`out` signatures copy pipeline outputs back to the caller.

`InterfaceImplementation` is shared by the analyzer and generator. `ST012` rejects abstract
interface members inaccessible to the consuming assembly, including internal setters:
omitting the setter produces `CS0535`, while explicitly implementing it produces `CS0122`.
Same-assembly and friend-assembly implementations remain supported, as do classes with an
existing concrete implementation. `ST012_InaccessibleInterfaceMember` tests the actual C#
compiler restriction using separate metadata assemblies.

`RuntimeSignature` similarly reports `ST013` for interceptable signatures using
`TypedReference`, `ArgIterator`, or `RuntimeArgumentHandle`, which cannot be represented by
the pipeline's boxed arguments or generic holders. Invalid candidates do not prevent unrelated
valid stunts from being generated.

## Comparison package acceptance coverage

`src/Acceptance/GenerateReferenceStunts.cs` is a .NET file-based app containing the stable
package snapshot used by the `Static` acceptance project (prerelease references and the cloned
`efcore` and `iotedge` repositories are intentionally excluded). Run
`dotnet run --file GenerateReferenceStunts.cs` from `src/Acceptance` to regenerate
`src/Acceptance/Static/Stunts.cs`. The generator always resolves that output relative to its own
source file. The file app disables `DeterministicSourcePaths` so CI path mapping does not
rewrite its `CallerFilePath` to `/_`; other projects retain deterministic source paths.
It loads the runtime assemblies from the comparison packages plus
`Microsoft.AspNetCore.App`, and writes a `Stunt.Of<T>()` call for every public interface and
non-sealed class that is compiler-visible and proxyable, is not marked as a preview feature,
and has no public preview members. Inaccessible abstract members, unavailable constructors,
static-abstract interfaces, special CLR signatures, and obsolete-as-error APIs are excluded.
Open generic types are emitted inside helper methods with their generic constraints preserved.

The script preserves its compilation context and reads public metadata names from compile
assets, filtering runtime-only types absent from reference assemblies. Match full metadata
names, not assembly names: runtime core types and their reference definitions live in different
assemblies. If changing file-based app properties leaves stale dependency metadata, rebuild
the script explicitly before running it.

Reflection type names preserve every declaring-type separator independently of generic arity.
Distribute the requested type's concrete generic arguments across its declaring-type chain;
`DeclaringType` itself reopens constructed generics. Map free parameters by identity and
preserve array ranks in C# order, including inside generic arguments and constraints.
`AcceptanceTypeNameTests` compiles the script's actual formatting helpers without executing
its package-loading entry point.

Keep the script directives and `src/Acceptance/Static/Stunts.props` synchronized when the
comparison projects change. The props file gives the acceptance compilations matching metadata
references; comparison-only references expose compile, runtime, and native assets, but not
build targets or analyzers. Runtime dependencies are required because xUnit reflects over the
generated types during discovery. A zero-test run is not successful acceptance validation:
verify that tests are actually discovered and executed.

Build and package the root solution before running `src/Acceptance/Acceptance.slnx`.
`src/Acceptance/nuget.config` maps `Stunts*` and `ManualStunts` to that `bin` feed so package
source mapping still restores the local packages.
The root solution lists `Static.csproj` as a solution item rather than a project:
acceptance consumes the produced packages, whose NuGet cache entries are cleared by
the package build, so including it in that build creates a bootstrap/cache-order cycle.

## Agent skills

### Issue tracker

Issues and specs live as GitHub issues in devlooped/stunts. See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical roles, each label string equal to its name: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
