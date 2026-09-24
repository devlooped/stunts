# Stunts API

Main entry point API is `Stunt.Of<T>`, which creates an object that implements `T`:

```csharp
ICalculator calculator = Stunt.Of<ICalculator>();

// Extension methods to easily add behaviors without having 
// to cast to IStunt
calculator.AddBehavior((invocation, next) => ...);

Console.WriteLine(calculator.Add(2, 5));
```

There are overloads for implementing additional types, as well as passing constructor 
arguments if the base type `T` (which must be the first in the list, like in regular 
C# type declarations) is a class that provides a constructor with matching parameters: 
`Stunt.Of<T, T1...Tn>(arg1, ... argn)`

For anonymous behaviors, the delegate/lambda based overloads are typically sufficient. 
For more advanced or reusable behaviors, you can implement `IStuntBehavior` instead.
