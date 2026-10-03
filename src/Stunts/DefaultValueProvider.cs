using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// Utility class that generates default values for certain types.
    /// Used by the <see cref="DefaultValueBehavior"/>.
    /// </summary>
    public class DefaultValueProvider
    {
#if NET8_0_OR_GREATER
        static readonly ConditionalWeakTable<Type, GeneratedDefault> generated = new();
#endif

        /// <summary>Registers a statically generated default value factory for Native AOT.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterGenerated<T>(Func<DefaultValueProvider, T> factory, Func<T> fallback)
        {
#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported)
                generated.GetValue(typeof(T), _ => new GeneratedDefault(provider => factory(provider), () => fallback()));
#endif
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        readonly ConcurrentDictionary<Type, Func<Type, object?>> factories = new ConcurrentDictionary<Type, Func<Type, object?>>();

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        /// <param name="registerDefaults">Whether to register the default value factories.</param>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reflection-based defaults are only registered when dynamic code is supported.")]
        public DefaultValueProvider(bool registerDefaults = true)
        {
            if (registerDefaults)
            {
                factories[typeof(Task)] = CreateTask;
                factories[typeof(ValueTask)] = CreateValueTask;
                factories[typeof(IEnumerable)] = CreateEnumerable;
#if NET8_0_OR_GREATER
                useGenerated = true;
                if (!RuntimeFeature.IsDynamicCodeSupported)
                    return;
#endif
                RegisterDynamicDefaults();
            }
        }

#if NET8_0_OR_GREATER
        readonly bool useGenerated;
        readonly ConcurrentDictionary<Type, byte> disabled = new();
#endif

        [RequiresDynamicCode("Default values for runtime types require generic instantiation. Register a typed factory for Native AOT.")]
        [RequiresUnreferencedCode("Default values for runtime types require reflection metadata. Register a typed factory for Native AOT.")]
        void RegisterDynamicDefaults()
        {
            factories[typeof(Array)] = CreateArray;
            factories[typeof(Task<>)] = CreateTaskOf;
            factories[typeof(ValueTask<>)] = CreateValueTaskOf;
            factories[typeof(IEnumerable<>)] = CreateEnumerableOf;
            factories[typeof(IQueryable)] = CreateQueryable;
            factories[typeof(IQueryable<>)] = CreateQueryableOf;
            factories[typeof(ValueTuple<>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,,,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,,,,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,,,,,>)] = CreateValueTupleOf;
            factories[typeof(ValueTuple<,,,,,,,>)] = CreateValueTupleOf;
        }

        /// <summary>
        /// Gets a default value for the given type <typeparamref name="T"/>.
        /// </summary>
        public T? GetDefault<T>() => (T?)GetDefault(typeof(T));

        /// <summary>
        /// Gets a default value for the given type <paramref name="type"/>
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Native AOT uses generated defaults and never calls the guarded reflection fallback.")]
        public object? GetDefault(Type type)
        {
            // If type is by ref, we need to get the actual element type of the ref. 
            // i.e. Object[]& has ElementType = Object[]
            var valueType = type.IsByRef && type.HasElementType ? type.GetElementType()! : type;
            var typeKey = valueType.IsArray ? typeof(Array) : valueType;

            // Try get a handler with the concrete type first.
            if (factories.TryGetValue(typeKey, out var factory))
                return factory.Invoke(valueType);

            // Fallback to getting one for the generic type, if available
            if (valueType.IsGenericType && factories.TryGetValue(valueType.GetGenericTypeDefinition(), out factory))
                return factory.Invoke(valueType);

#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported && useGenerated &&
                BuiltInKey(valueType) is Type key && !disabled.ContainsKey(key) && generated.TryGetValue(valueType, out var typed))
                return typed.Create(this);
#endif
            return GetFallbackDefaultValue(valueType);
        }

        /// <summary>
        /// Deregisters a default value factory for the given <paramref name="key"/>.
        /// </summary>
        /// <returns>Whether there was a registered factory and it was removed.</returns>
        public bool Deregister(Type key)
        {
            var removed = factories.TryRemove(key, out _);
#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported && useGenerated && BuiltInKey(key) == key)
                return disabled.TryAdd(key, 0) || removed;
#endif
            return removed;
        }

        /// <summary>
        /// Registers a default value factory for the given <paramref name="key"/>.
        /// </summary>
        public void Register(Type key, Func<Type, object> factory) => factories[key] = factory;

        /// <summary>
        /// Deregisters a default value factory for the given <typeparamref name="T"/>.
        /// </summary>
        /// <returns>Whether there was a registered factory and it was removed.</returns>
        public bool Deregister<T>() => Deregister(typeof(T));

        /// <summary>
        /// Registers a default value factory for the given <typeparamref name="T"/>.
        /// </summary>
        public void Register<T>(Func<T> factory) => Register(typeof(T), _ => factory()!);

        /// <summary>
        /// Determines the default value for the given <paramref name="type"/> when no suitable factory is registered for it.
        /// </summary>
        /// <param name="type">The type of which to produce a value.</param>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The reflection fallback is unreachable when dynamic code is unavailable.")]
        protected virtual object? GetFallbackDefaultValue(Type type)
        {
#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                if (generated.TryGetValue(type, out var typed))
                    return typed.Fallback();
                if (type.IsValueType || useGenerated && BuiltInKey(type) is Type key && !disabled.ContainsKey(key))
                    throw new NotSupportedException($"No default value factory is registered for '{type}'. Register a typed factory with DefaultValueProvider.Register<T>().");
                return null;
            }
#endif
            return GetDynamicFallback(type);
        }

#if NET8_0_OR_GREATER
        static Type? BuiltInKey(Type type)
        {
            if (type.IsArray || type == typeof(Array))
                return typeof(Array);
            var key = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (key == typeof(Task) || key == typeof(ValueTask) || key == typeof(IEnumerable) || key == typeof(IQueryable) ||
                key == typeof(Task<>) || key == typeof(ValueTask<>) || key == typeof(IEnumerable<>) || key == typeof(IQueryable<>) ||
                key == typeof(ValueTuple<>) || key == typeof(ValueTuple<,>) || key == typeof(ValueTuple<,,>) ||
                key == typeof(ValueTuple<,,,>) || key == typeof(ValueTuple<,,,,>) || key == typeof(ValueTuple<,,,,,>) ||
                key == typeof(ValueTuple<,,,,,,>) || key == typeof(ValueTuple<,,,,,,,>))
                return key;
            return null;
        }

        sealed class GeneratedDefault
        {
            public GeneratedDefault(Func<DefaultValueProvider, object?> create, Func<object?> fallback)
                => (Create, Fallback) = (create, fallback);

            public Func<DefaultValueProvider, object?> Create { get; }
            public Func<object?> Fallback { get; }
        }
#endif

        [RequiresUnreferencedCode("Default value construction requires the runtime type's constructor.")]
        static object? GetDynamicFallback(Type type)
        {
            if (type.IsValueType)
            {
                // For nullable value types, return null.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return null;

                return Activator.CreateInstance(type);
            }

            return null;
        }

        [RequiresDynamicCode("Array defaults require the element type's array instantiation. Register a typed factory for Native AOT.")]
        static object CreateArray(Type type) => Array.CreateInstance(
            type.GetElementType() ?? throw new ArgumentException(nameof(type)), new int[type.GetArrayRank()]);

        static object CreateEnumerable(Type type) => Enumerable.Empty<object>();

        [RequiresDynamicCode("Enumerable defaults require the element type's array instantiation.")]
        static object CreateEnumerableOf(Type type) => Array.CreateInstance(type.GenericTypeArguments[0], 0);

        [RequiresDynamicCode("Queryable defaults require runtime generic instantiation.")]
        [RequiresUnreferencedCode("Queryable defaults require reflection metadata.")]
        static object CreateQueryable(Type type) => Enumerable.Empty<object>().AsQueryable();

        [RequiresDynamicCode("Queryable defaults require runtime generic instantiation.")]
        [RequiresUnreferencedCode("Queryable defaults require reflection metadata.")]
        static object? CreateQueryableOf(Type type)
        {
            var elementType = type.GetGenericArguments()[0];
            var array = Array.CreateInstance(elementType, 0);

            return typeof(Queryable).GetMethods()
                .Single(x => x.Name == nameof(Queryable.AsQueryable) && x.IsGenericMethod)
                .MakeGenericMethod(elementType)
                .Invoke(null, new[] { array });
        }

        [RequiresUnreferencedCode("Tuple defaults require constructor metadata.")]
        object? CreateValueTupleOf(Type type)
        {
            var itemTypes = type.GetGenericArguments();
            var items = new object?[itemTypes.Length];
            for (int i = 0, n = itemTypes.Length; i < n; ++i)
            {
                items[i] = GetDefault(itemTypes[i]);
            }

            return Activator.CreateInstance(type, items);

        }

        // See https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/ValueTask.cs#L114
        static object CreateValueTask(Type type) => default(ValueTask);

        [RequiresUnreferencedCode("ValueTask defaults require constructor metadata.")]
        object CreateValueTaskOf(Type type)
        {
            var resultType = type.GetGenericArguments()[0];
            // A null result would also match the ValueTask<T>(Task<T>) constructor.
            var constructor = type.GetConstructor(new[] { resultType }) ?? throw new NotSupportedException();

            return constructor.Invoke(new[] { GetDefault(resultType) });
        }

        static object CreateTask(Type type) => Task.CompletedTask;

        [RequiresDynamicCode("Task defaults require runtime generic instantiation.")]
        [RequiresUnreferencedCode("Task defaults require reflection metadata.")]
        object CreateTaskOf(Type type) => GetCompletedTaskForType(type.GenericTypeArguments[0]);

        [RequiresDynamicCode("Task defaults require runtime generic instantiation.")]
        [RequiresUnreferencedCode("Task defaults require reflection metadata.")]
        Task GetCompletedTaskForType(Type type)
        {
            var tcs = Activator.CreateInstance(typeof(TaskCompletionSource<>).MakeGenericType(type))
                ?? throw new NotSupportedException();
            var setResultMethod = tcs.GetType().GetMethod(nameof(TaskCompletionSource<object>.SetResult))
                ?? throw new NotSupportedException();
            var taskProperty = tcs.GetType().GetProperty(nameof(TaskCompletionSource<object>.Task))
                ?? throw new NotSupportedException();

            var result = GetDefault(type);
            setResultMethod.Invoke(tcs, new[] { result });

            return (Task?)taskProperty.GetValue(tcs, null) ?? throw new NotSupportedException();
        }
    }
}
