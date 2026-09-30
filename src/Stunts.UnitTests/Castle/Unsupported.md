# Castle DynamicProxy scenarios Stunts does not support

This is about compile-time stunts (`Stunt.Of` and the source generator). `Stunts.DynamicProxy` is a Castle backend, so a scenario working there only means Castle still works.

The tests next to this file are the Castle scenarios the generator can carry. They run through the scenario host in `.Scenarios.cs`, which compiles each file with `StuntGenerator` and executes `IRunnable.Run`.

## Supported, and covered by the tests

| Castle scenario | Test |
| --- | --- |
| Class proxy: virtual proceed, most-derived override, abstract members, non-virtual left alone | `ClassProxyTests` |
| Self-calls hit the override (Castle's "leaking this" for class proxies) | `ClassProxyTests` |
| Constructors: public, protected, internal (same assembly), `params`, null argument | `ClassProxyTests` |
| Virtual call during construction, when the behavior is installed with `BehaviorPipelineFactory` before `Stunt.Of` | `ClassProxyTests` |
| Protected virtual methods, nested classes, internal classes, internal and private protected members, private nested types inside a partial container, replicated parameter names | `ClassProxyTests` |
| Interface proxy without a target | `InterfaceProxyTests` |
| Inherited interfaces, additional interfaces, class as the base of extra interfaces | `InterfaceProxyTests` |
| Same generic interface twice (`ISlot<int>` and `ISlot<string>`), explicit implementation when signatures collide | `InterfaceProxyTests` |
| `IEnumerable` / `IEnumerable<T>` `GetEnumerator` | `InterfaceProxyTests` |
| Non-virtual interface implementation is not re-intercepted; virtual implementation is | `InterfaceProxyTests` |
| Default interface members are intercepted and proceed to the default through the generated `Default{Interface}` class | `Scenarios/DefaultInterfaceImplementation` |
| Interface with a target, and swapping that target for the lifetime of one proxy | `CompositionTests` |
| Class proxy forwarding virtual calls to another instance (non-virtual state stays on the proxy, the same caveat Castle documents) | `CompositionTests` |
| Mixin-style forwarding of each extra interface to its own instance | `CompositionTests` |
| Properties, setter-only properties, indexers, virtual events, interface event add/remove, case-sensitive members, narrowed accessors (`protected` / `internal` get or set) | `MembersTests` |
| `ref` / `out`, `in` on a concrete struct, `params`, multidimensional arrays, nullable and `decimal` outs | `ParametersTests` |
| Closed generic classes and interfaces, generic constraints (`class`, `new()`, `struct`, `unmanaged`, `Enum`, `notnull`, base type) | `GenericsTests` |
| Generic methods, including `in T` and `out T`; the invocation carries the constructed method | `GenericsTests` |
| Pipeline order, short-circuit, proceed twice (retry), `AppliesTo` as an interceptor selector, catch and replace a target exception, invocation target / method / arguments | `PipelineTests` |
| Async: return a task, and compose the task produced by proceed | `AsyncTests` |
| Covariant returns | `LanguageFeaturesTests` |
| Records (positional, empty, record plus an extra interface; `with` keeps the stunt type) | `LanguageFeaturesTests` |
| `ref readonly`, `Span<T>` and other `ref struct` arguments and returns, pointer parameters | `Scenarios/SignatureRefs` |
| Custom attributes on generated types, members, parameters, and returns; optional parameter defaults (including reflection invocation) | `AttributeTests` |

Behaviors added with `AddBehavior` after `Stunt.Of` do not see virtual calls made from a base constructor. Castle passes interceptors into proxy creation, so they do. `BehaviorPipelineFactory` is the matching extension point, and `ClassProxyTests` uses it.

## Gaps worth deciding on

These are Castle features a caller can observe. They are the ones to accept or build.

### No way to leave a member unproxied

Castle's `IProxyGenerationHook` omits members from the proxy type. Stunts overrides every accessible virtual, abstract, and interface member. A behavior can ignore a call, but the override still exists, so a self-call inside the base class is intercepted. There is no equivalent of "non-proxied target methods".

`AppliesTo` covers Castle's `IInterceptorSelector` (which methods run which behaviors). That part is tested.

### Metadata Castle copies onto the proxy

The generator copies C#-expressible metadata, including custom attributes and optional
parameter default values (covered by `AttributeTests`). Remaining metadata gaps are:

- metadata-only custom modifiers (`modopt` / `modreq`) with no C# spelling
- extra attributes supplied at generation time (`ProxyGenerationOptions.AdditionalAttributes`)

### Serialization

Castle-specific proxy serialization (`ISerializable`, restoring interceptors, mixins,
selector, and generation options, including graphs with delegates back to the proxy) is
not implemented by compile-time stunts. Copying `[Serializable]` and other attributes
does not provide that proxy-serialization infrastructure.

### Delegates as additional type arguments

`Stunt.Of<SomeDelegate>()` returns a delegate bound to a generated `Invoke`, and `Stunt.Of<SomeDelegate>(implementation)` forwards `next` to that delegate. A delegate combined with a class or another interface is `ST010`. Castle can also mix a delegate `Invoke` onto some other proxy (`AsDelegate` is the missing cast).

### Cannot distinguish identical interface slots

Identical signatures from two interfaces share one generated implementation. Both calls
are intercepted, but a behavior cannot tell which interface slot was called. Castle can
emit explicit implementations so `IInvocation.Method` differs.

### Fixed additional-interface arity

`Stunt.Of` accepts at most eight additional interfaces. Castle takes a `Type[]`.

## Not a Stunts feature

Castle tests that cover the Reflection.Emit generator rather than proxy behavior. Compile-time generation replaces them; there is nothing to port unless Stunts grows a runtime emitter of its own.

- `ModuleScope`, persistent proxy builder, saved assemblies, multiple assemblies
- Proxy type cache keys, hook equality, invocation-type reuse
- `ClassEmitter`, tokens, `TypeUtil`, method signature comparer, PEVerify, generator logging
- `IProxyTargetAccessor` / `IChangeProxyTarget` as built-in interfaces (swapping a target is a behavior, and `CompositionTests` does that)
- `ProxyUtil.CreateDelegateToMixin`, `MixinData`, generation-options equality

Castle also cannot proxy sealed classes, enums, or structs, and cannot intercept non-virtual methods. Stunts matches that (`ST003`, `ST004`, and non-virtual members left on the base). Open generics are rejected by the `Stunt.Of<T>` call site.

Visual Basic generation is still a stub (`ProcessorContext.Language` is C# only). Castle generates proxies the language of the caller does not affect.
