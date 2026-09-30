# Stunts API

Main entry point API is `Stunt.Of<T>`, which creates an object that implements `T`:

```csharp
ICalculator calculator = Stunt.Of<ICalculator>();

Console.WriteLine(calculator.Add(2, 5));
```

Behaviors are configured through `IStunt`, which every stunt implements. `Stunt.For<T>` 
creates the stunt and returns a `StuntReference<T>` that exposes both the behaviors and 
the stunt itself (via `ToObject()`, or an implicit conversion for non-interface types):

```csharp
var stunt = Stunt.For<ICalculator>();
ICalculator calculator = stunt.ToObject();

stunt.AddBehavior((invocation, next) => ...);

Console.WriteLine(calculator.Add(2, 5));
```

`Stunt.Get(calculator)` returns the same `StuntReference<T>` for an already created stunt, 
so behaviors can be added after the fact.

There are overloads for implementing additional types, as well as passing constructor 
arguments if the base type `T` (which must be the first in the list, like in regular 
C# type declarations) is a class that provides a constructor with matching parameters: 
`Stunt.Of<T, T1...Tn>(arg1, ... argn)` and `Stunt.For<T, T1...Tn>(arg1, ... argn)`

For anonymous behaviors, the delegate/lambda based overloads are typically sufficient. 
For more advanced or reusable behaviors, you can implement `IStuntBehavior` instead.
