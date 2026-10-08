<p align="center">
  <img src="https://github.com/devlooped/stunts/raw/main/docs/images/hero.jpg" alt="Stunts" width="256">
</p>

<h1 id="stunts" align="center">Stunts</h1>

<p align="center">
  <a href="https://www.nuget.org/packages/Stunts"><img src="https://img.shields.io/nuget/vpre/Stunts.svg?color=royalblue" alt="Version"></a>
  <a href="https://www.nuget.org/packages/Stunts"><img src="https://img.shields.io/nuget/dt/Stunts?color=darkmagenta" alt="Downloads"></a>
  <a href="osmfeula.txt"><img src="https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&amp;color=C9FF30" alt="EULA"></a>
  <a href="license.txt"><img src="https://img.shields.io/github/license/devlooped/oss.svg?color=blue" alt="OSS"></a>
  <a href="https://discord.gg/zETqV7HQ3Q"><img src="https://img.shields.io/badge/chat-on%20discord-7289DA.svg" alt="Discord Chat"></a>
</p>

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->
## Open Source Maintenance Fee

To ensure the long-term sustainability of this project, users of this package who generate 
revenue must pay an [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). 
While the source code is freely available under the terms of the [License](license.txt), 
this package and other aspects of the project require [adherence to the Maintenance Fee](osmfeula.txt).

To pay the Maintenance Fee, [become a Sponsor](https://github.com/sponsors/devlooped) at the proper 
OSMF tier. A single fee covers all of [Devlooped packages](https://www.nuget.org/profiles/Devlooped).

<!-- https://github.com/devlooped/.github/raw/main/osmf.md -->
---
<!-- #content -->
A modern interception library that runs everywhere, even where run-time code generation (Reflection.Emit) is forbidden or limitted (i.e. physical iOS devices and game consoles), through compile-time code generation.

> The **Stunts** name was inspired by the [Test Double](http://xunitpatterns.com/Test%20Double.html) naming in the mock objects literature, which in turn comes from the [Stunt Double](https://en.wikipedia.org/wiki/Stunt_double) concept from film making. This project allows your objects to pull arbitrary *stunts* based on your instructions/choreography 😉.

Stunts essentially implements the [proxy pattern](https://en.wikipedia.org/wiki/Proxy_pattern) and adds the capability of configuring those proxies via code using what we call a *behavior pipeline*.

> NOTE: Stunts provides a fairly low-level API with just the essential building blocks on top of which higher-level APIs can be built, such as the upcoming Moq vNext API.

## Usage

```csharp
var stunt = Stunt.For<ICalculator>();
ICalculator calc = stunt.ToObject();

stunt.AddBehavior((invocation, next) => ...);
```

`Stunt.Of<T>` returns the stunt directly, and `Stunt.Get(stunt)` gets a `StuntReference<T>` for an existing one, so behaviors can be added after the fact:

```csharp
ICalculator calc = Stunt.Of<ICalculator>();

Stunt.Get(calc).AddBehavior((invocation, next) => ...);
```

> NOTE: `StuntReference<T>` converts implicitly to `T` for classes and delegates. C# does not allow user-defined conversions to interfaces, so `ToObject()` is always available.

`AddBehavior`/`InsertBehavior` are extension methods on `IStunt` (which `StuntReference<T>` implements) and allow granular control of the stunt's behavior pipeline, which is basically a [chain of responsibility](https://en.wikipedia.org/wiki/Chain-of-responsibility_pattern) that invokes all configured behaviors that apply to the current invocation. Individual behaviors can determine whether to short-circuit the call or call the next behavior in the chain.

Behaviors can also dynamically determine whether they apply to a given invocation by providing the optional `appliesTo` argument. In addition to the delegate-based overloads (called *anonymous behaviors*), you can also create behaviors by implementing the `IStuntBehavior` interface:

```csharp
public interface IStuntBehavior
{
    bool AppliesTo(IMethodInvocation invocation);
    IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next);
}
```

## Common Behaviors

Some commonly used behaviors that are generally useful are provided in the library and can be added to stunts as needed:

* `DefaultValueBehavior`: sets default values for method return and *out* arguments. In addition to the built-in supported default values, additional default value factories can be registered for any type.

* `DefaultEqualityBehavior`: implements the *Object.Equals* and *Object.GetHashCode* members just like *System.Object* implements them.

* `RecordingBehavior`: simple behavior that keeps track of all invocations, for troubleshooting or reporting.

* `ExceptionMappingBehavior`: translates an exception from the target. Return null from the map to leave the exception unchanged, or another instance to replace it. Pass `swallow: true` to turn a null result into the member's default value.

## Building Stunts

When you need the same behaviors on multiple stunts, `Stunt.Builder()` returns a `StuntBuilder` 
that collects behaviors (with the very same `AddBehavior`/`InsertBehavior` extension methods) and 
applies them to every stunt it builds, with the same `Build<T>` overloads as `Stunt.Of<T>`:

```csharp
var builder = Stunt.Builder()
    .AddBehavior(new RecordingBehavior())
    .AddBehavior(new DefaultValueBehavior());

ICalculator calculator = builder.Build<ICalculator>();
IStore store = builder.Build<IStore>();
```

Since the behaviors are in place *before* the stunt is instantiated (via an ambient 
`BehaviorPipelineFactory`), they also intercept virtual members invoked from base class 
constructors, which isn't possible when behaviors are added to an already created stunt:

```csharp
public class Greeter
{
    public Greeter() => Seen = Name();
    public string Seen { get; }
    public virtual string Name() => "base";
}

Greeter greeter = Stunt.Builder()
    .AddBehavior((invocation, next) => invocation.MethodBase.Name == nameof(Greeter.Name)
        ? invocation.CreateValueReturn("proxy")
        : next(invocation, next))
    .Build<Greeter>();

// greeter.Seen == "proxy"
```

Each `Build` call takes a snapshot of the behaviors configured at that point, so behaviors added 
to the builder afterwards don't affect the stunts already built. Behavior instances themselves are 
shared, so a single `RecordingBehavior` records the invocations of all stunts from that builder.

## Customizing Stunt Creation

If you want to centrally configure all your stunts, the easiest way is to simply provide your own factory method (i.e. `Stub.Of<T>`), which in turn calls the `Stunt.Of<T>` provided. For example:

```csharp
    public static class Stub
    {
        [StuntGenerator]
        public static T Of<T>() => Stunt.For<T>()
            .AddBehavior(new RecordingBehavior())
            .AddBehavior(new DefaultEqualityBehavior())
            .AddBehavior(new DefaultValueBehavior())
            .ToObject();
    }
```

The `[StuntGenerator]` attribute is required if you want to leverage the built-in compile-time code generation, since that signals to the source generator that calls to your API end up creating a stunt at run-time and therefore a generated type will be needed for it during compile-time. You can actually explore how this very same behavior is implemented in the built-in Stunts API, which is provided as content files (`Stunt.cs` and `Stunt.vb`):

```csharp
[StuntGenerator]
public static T Of<T>(params object[] constructorArgs) => Create<T>(constructorArgs);

[StuntGenerator]
public static T Of<T, T1>(params object[] constructorArgs) => Create<T>(constructorArgs, typeof(T1));
```

As you can see, the Stunts API itself uses the same extensibility mechanism that your own custom factory methods can use.

The attribute normally treats the generic arguments at the call site as the stunt's base type and
additional interfaces. Generic factories that construct stunt types from their own type parameters
must be annotated too:

```csharp
[StuntGenerator]
static IDictionary<string, T> Create<T>() => Stunt.Of<IDictionary<string, T>>();

IDictionary<string, int> dictionary = Create<int>();
```

The generator follows such wrappers (up to 8 levels, including across referenced assemblies) and
generates the closed stunts each call site needs, here `IDictionary<string, int>`. Unannotated
wrappers are reported (`ST016`, with a code fix). Stunt types only known at run time can be
registered explicitly anywhere in the project:

```csharp
[assembly: Stunt<IDictionary<string, DateTime>, IDisposable>]
```

Requesting a stunt that was not generated throws a `NotSupportedException` containing the exact
attribute to add.

### Native AOT

Compile-time stunts support Native AOT on .NET 8 and later. Generated registrations
preserve constructors and invocation metadata, and generated typed factories
provide `DefaultValueBehavior` defaults without runtime generic instantiation. Interfaces,
classes, delegates, additional interfaces, constructor interception, default interface members,
and closed generic targets are supported.
Ref-struct holders additionally require .NET 9 or later for by-ref-like generics.
Ref-struct and pointer signatures require unsafe blocks.

Set `<PublishAot>true</PublishAot>` to publish a native executable. `IsAotCompatible` or
`EnableAotAnalyzer` also enables the Stunts compatibility warnings during ordinary builds:

| Warning | Limitation | Alternative |
| --- | --- | --- |
| `ST014` | Runtime proxies require dynamic code | Enable compile-time stunts |
| `ST015` | Generic intercepted methods use runtime `MakeGenericMethod` | Use non-generic members with closed parameter and return types |
| `ST015` | Ref-struct interception needs by-ref-like generics | Target .NET 9 or later |
| `ST015` | `IQueryable` defaults require dynamic code | Register an AOT-compatible typed `DefaultValueProvider` factory, or avoid `DefaultValueBehavior` for that member |

Generated stunts register typed constructors from a module initializer, so they are created
without reflection. Use `Ref.Create<T>` instead of the reflection-based `Ref.Create(Type, object)`
overload, which carries the standard .NET AOT and trimming warnings.

Arrays, enumerable results, tasks, value tasks, tuples, and output defaults used by closed
stunts have typed factories. Defaults for other runtime-only types require explicit
`DefaultValueProvider.Register<T>` factories. Custom registrations, deregistration, and virtual
fallback overrides remain available.

### Compiled vs Dynamic Stunts

By default, Stunts generates proxies at compile-time (powered by Roslyn source generators). Whenever compile-time stunts are 
not supported (or unwanted), install the `Stunts.DynamicProxy` package, which switches the project to run-time proxies based on Castle.Core:

```xml
<ItemGroup>
    <PackageReference Include="Stunts.DynamicProxy" Version="..." />
</ItemGroup>
```

The package sets `EnableCompileTimeStunts=false` for you. Projects that can't use compile-time stunts and don't reference `Stunts.DynamicProxy` get a build warning (`ST011`).
Visual Basic projects only support run-time stunts, so they always need `Stunts.DynamicProxy`.

Compile-time stunts support optional parameters, inherited generic constraints, long signatures,
and `ref`/`out` arguments alongside spans and other ref structs. Ref-struct and pointer signatures
require unsafe blocks to be enabled.

Unsupported targets produce diagnostics at the factory call: `ST012` identifies inaccessible
abstract interface members (such as an internal setter from another assembly, which C# cannot
implement), while `ST013` identifies signatures using `TypedReference`, `ArgIterator`, or
`RuntimeArgumentHandle`, which the behavior pipeline cannot represent. Internal interface
members remain supported when accessible through the same assembly or `InternalsVisibleTo`.

<!-- #manual -->
> NOTE: even though generated proxies are the main usage for Stunts, the API was designed so that you can also consume the behavior pipeline easily from hand-coded proxies too.
<!-- #manual -->

## Features

The examples below use `ICalculator` from [the samples](samples/Samples/Core/ICalculator.cs), which declares `Add(int x, int y)`. Each snippet starts with a fresh stunt.

### Return a value for a specific call

An anonymous behavior can short-circuit a call. The `appliesTo` predicate limits it to the two-argument `Add` overload:

```csharp
var calc = Stunt.For<ICalculator>().AddBehavior(
    (call, _) => call.CreateValueReturn(42),
    call => call.MethodBase.Name == nameof(ICalculator.Add) && call.Arguments.Count == 2)
    .ToObject();

calc.Add(2, 3); // 42
```

### Compute a result from the arguments

Arguments are available by name (or index), so a behavior can use the values passed by the caller:

```csharp
var calc = Stunt.For<ICalculator>().AddBehavior(
    (call, _) => call.CreateValueReturn(call.Arguments.Get<int>("x") + call.Arguments.Get<int>("y")),
    call => call.MethodBase.Name == nameof(ICalculator.Add) && call.Arguments.Count == 2)
    .ToObject();

calc.Add(2, 3); // 5
```

### Compose behaviors

Behaviors run in order. Put recording first to capture calls and results, and a default-value behavior last to handle calls not matched by the `Add` behavior:

```csharp
var recorder = new RecordingBehavior();
var calc = Stunt.For<ICalculator>()
    .AddBehavior(recorder)
    .AddBehavior((call, _) => call.CreateValueReturn(5),
        call => call.MethodBase.Name == nameof(ICalculator.Add) && call.Arguments.Count == 2)
    .AddBehavior(new DefaultValueBehavior())
    .ToObject();

calc.Add(2, 3); // 5
var calls = recorder.Invocations.Count; // 1
```

### Customize default values

Register a factory when the built-in defaults are not suitable. Here, each call to a delegate stunt returns a greeting:

```csharp
var defaults = new DefaultValueProvider();
defaults.Register(() => "Hello!");
var greet = Stunt.For<Func<string>>().AddBehavior(new DefaultValueBehavior(defaults)).ToObject();

greet(); // "Hello!"
```

### Intercept a real implementation

Pass a delegate implementation to `Stunt.For` and call `next` to forward to it. Behaviors can change arguments before forwarding:

```csharp
var add = Stunt.For<Func<int, int, int>>((x, y) => x + y).AddBehavior((call, next) =>
{
    call.Arguments.Set(0, 10);
    return next(call, next);
}).ToObject();

add(1, 2); // 12
```

### Debugging Optimizations

There is nothing more frustrating than a proxy/stunt you have carefully configured that doesn't behave the way you expect it to. In order to make this a less frustrating experience, Stunts is carefully optimized for debugger display and inspection, so that it's clear what behaviors are configured, and invocations and results are displayed clearly and concisely. Here's the debugging display of the `RecordingBehavior` that just keeps track of invocations and their return values for example:

![debugging display](docs/images/DebuggerDisplay.png)

And here's the invocation debugger display from an anonymous behavior:

![behavior debugging](docs/images/DebuggingBehavior.png)

<!-- #samples -->
## Samples

The `samples` folder in the repository contains a few interesting examples of how *Stunts* can be used to implement some fancy use cases. For example:

* Forwarding calls to matching interface methods/properties (by signature) to a static class. The example uses this to wrap calls to *System.Console* via an *IConsole* interface.

* Forwarding calls to a target object using the DLR (that backs the *dynamic* keyword in C#) API for high-performance late binding.

* Custom `Stub.Of<T>` factory that creates stunts that have common behaviors configured automatically.

* Custom stunt factory method that adds an int return value randomizer.

* Configuring the built-in *DefaultValueBehavior* so that every time a string property is retrieved, it gets a random lorem ipsum value.

* Logging all calls to a stunt to the Xunit output helper.
<!-- #samples -->
<!-- #content -->

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![Ryan McCaffery](https://avatars.githubusercontent.com/u/16667079?u=c0daa64bb5c1b572130e05ae2b6f609ecc912d4d&v=4&s=39 "Ryan McCaffery")](https://github.com/mccaffers)
[![Seika Logiciel](https://avatars.githubusercontent.com/u/2564602?v=4&s=39 "Seika Logiciel")](https://github.com/SeikaLogiciel)
[![Andrew Grant](https://avatars.githubusercontent.com/devlooped-user?s=39 "Andrew Grant")](https://github.com/wizardness)
[![eska-gmbh](https://avatars.githubusercontent.com/devlooped-team?s=39 "eska-gmbh")](https://github.com/eska-gmbh)
[![Geodata AS](https://avatars.githubusercontent.com/u/5946299?v=4&s=39 "Geodata AS")](https://github.com/geodata-no)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
