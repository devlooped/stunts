[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](osmfeula.txt)
[![OSS](https://img.shields.io/github/license/devlooped/oss.svg?color=blue)](license.txt)
[![GitHub](https://img.shields.io/badge/-source-181717.svg?logo=GitHub)](https://github.com/devlooped/stunts)

<!-- include ../../readme.md#content -->
<!-- #content -->
A modern interception library that runs everywhere, even where run-time code generation (Reflection.Emit) is forbidden or limitted (i.e. physical iOS devices and game consoles), through compile-time code generation.

> The **Stunts** name was inspired by the [Test Double](http://xunitpatterns.com/Test%20Double.html) naming in the mock objects literature, which in turn comes from the [Stunt Double](https://en.wikipedia.org/wiki/Stunt_double) concept from film making. This project allows your objects to pull arbitrary *stunts* based on your instructions/choreography 😉.

Stunts essentially implements the [proxy pattern](https://en.wikipedia.org/wiki/Proxy_pattern) and adds the capability of configuring those proxies via code using what we call a *behavior pipeline*.

> NOTE: Stunts provides a fairly low-level API with just the essential building blocks on top of which higher-level APIs can be built, such as the upcoming Moq vNext API.

## Requirements

Stunts is a .NET Standard 2.0 library and runs on any runtime that supports that.

Compile-time proxy generation leverages [Roslyn source generators](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.cookbook.md) and therefore requires C# 9.0, which at this time is included in Visual Studio 16.8 (preview or later) and the .NET 5.0 SDK (RC or later). Compile-time generated proxies support the broadest possible run-time platforms since they don't require any Reflection.Emit, and also don't pay that performance cost either.

Whenever compile-time proxy generation is not available (i.e. Visual Basic or C# versions before 9.0), install the `Stunts.DynamicProxy` package instead, which leverages [Castle DynamicProxy](https://github.com/castleproject/Core/blob/master/docs/dynamicproxy-introduction.md) to provide the run-time code generation.

The client API for configuring proxy behaviors in either case is exactly the same.

<!-- #manual -->
> NOTE: even though generated proxies are the main usage for Stunts, the API was designed so that you can also consume the behavior pipeline easily from hand-coded proxies too.
<!-- #manual -->

## Usage

```csharp
ICalculator calc = Stunt.Of<ICalculator>();

calc.AddBehavior((invocation, next) => ...);
```

`AddBehavior`/`InsertBehavior` overloads allow granular control of the stunt's behavior pipeline, which is basically a [chain of responsibility](https://en.wikipedia.org/wiki/Chain-of-responsibility_pattern) that invokes all configured behaviors that apply to the current invocation. Individual behaviors can determine whether to short-circuit the call or call the next behavior in the chain.

![Stunts Overloads](https://github.com/devlooped/stunts/raw/main/docs/images/AddInsertBehavior.png)

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

## Customizing Stunt Creation

If you want to centrally configure all your stunts, the easiest way is to simply provide your own factory method (i.e. `Stub.Of<T>`), which in turn calls the `Stunt.Of<T>` provided. For example:

```csharp
    public static class Stub
    {
        [StuntGenerator]
        public static T Of<T>() => Stunt.Of<T>()
            .AddBehavior(new RecordingBehavior())
            .AddBehavior(new DefaultEqualityBehavior())
            .AddBehavior(new DefaultValueBehavior());
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

### Static vs Dynamic Stunts

By default, Stunts generates proxies at compile-time (powered by Roslyn source generators), which is only supported when building C# 9.0+ projects.

Whenever compile-time stunts are not supported (or not wanted), install the `Stunts.DynamicProxy` package, which switches the project to run-time proxies based on Castle.Core:

```xml
<ItemGroup>
    <PackageReference Include="Stunts.DynamicProxy" Version="..." />
</ItemGroup>
```

The package sets `EnableCompileTimeStunts=false` for you. Projects that can't use compile-time stunts and don't reference `Stunts.DynamicProxy` get a build warning (`ST011`).

## Debugging Optimizations

There is nothing more frustrating than a proxy/stunt you have carefully configured that doesn't behave the way you expect it to. In order to make this a less frustrating experience, Stunts is carefully optimized for debugger display and inspection, so that it's clear what behaviors are configured, and invocations and results are displayed clearly and concisely. Here's the debugging display of the `RecordingBehavior` that just keeps track of invocations and their return values for example:

![debugging display](https://github.com/devlooped/stunts/raw/main/docs/images/DebuggerDisplay.png)

And here's the invocation debugger display from an anonymous behavior:

![behavior debugging](https://github.com/devlooped/stunts/raw/main/docs/images/DebuggingBehavior.png)

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
<!-- ../../readme.md#content -->

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->
## Open Source Maintenance Fee

To ensure the long-term sustainability of this project, users of this package who generate 
revenue must pay an [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). 
While the source code is freely available under the terms of the [License](license.txt), 
this package and other aspects of the project require [adherence to the Maintenance Fee](osmfeula.txt).

To pay the Maintenance Fee, [become a Sponsor](https://github.com/sponsors/devlooped) at the proper 
OSMF tier. A single fee covers all of [Devlooped packages](https://www.nuget.org/profiles/Devlooped).

<!-- https://github.com/devlooped/.github/raw/main/osmf.md -->

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
