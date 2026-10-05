using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// Typed adapters that await and rebuild <see cref="Task{TResult}"/> and <see cref="ValueTask{TResult}"/>.
    /// The source generator calls <see cref="Register{T}"/> for every closed awaitable a stunt returns.
    /// </summary>
    public static class AsyncRegistry
    {
        static readonly ConcurrentDictionary<Type, Adapter> adapters = new();

        static AsyncRegistry()
        {
            adapters[typeof(Task)] = Adapter.PlainTask();
            adapters[typeof(ValueTask)] = Adapter.PlainValueTask();
        }

        /// <summary>
        /// Registers await and wrap adapters for <see cref="Task{TResult}"/> and <see cref="ValueTask{TResult}"/>.
        /// </summary>
        /// <typeparam name="T">The result type carried by the awaitable.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register<T>()
        {
            adapters.TryAdd(typeof(Task<T>), Adapter.TaskOf<T>());
            adapters.TryAdd(typeof(ValueTask<T>), Adapter.ValueTaskOf<T>());
        }

        internal static Type? AwaitableReturn(MethodBase method)
        {
            if (method is not MethodInfo info || info.ReturnType == typeof(void) || info.ReturnType.IsByRef)
                return null;

            var type = info.ReturnType;
            if (type == typeof(Task) || type == typeof(ValueTask))
                return type;
            if (!type.IsGenericType)
                return null;

            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
                return type;

            return null;
        }

        internal static ValueTask<(object? Value, Exception? Error)> Unwrap(Type awaitable, object instance)
            => Require(awaitable).Unwrap(instance);

        internal static object Start(Type awaitable, Task<(object? Value, Exception? Error)> pending)
            => Require(awaitable).Start(pending);

        internal static object FromOutcome(Type awaitable, ProceedOutcome outcome)
            => Start(awaitable, Task.FromResult((outcome.Value, outcome.Exception)));

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reflection runs only when dynamic code is supported.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection runs only when dynamic code is supported.")]
        static Adapter Require(Type awaitable)
        {
            if (adapters.TryGetValue(awaitable, out var adapter))
                return adapter;

#if NET8_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported)
                throw NotRegistered(awaitable);
#endif
            RegisterDynamic(awaitable);
            return adapters[awaitable];
        }

        [RequiresDynamicCode("Closed task adapters are generated. Call AsyncRegistry.Register<T>() for Native AOT.")]
        [RequiresUnreferencedCode("Closed task adapters are generated. Call AsyncRegistry.Register<T>() for Native AOT.")]
        static void RegisterDynamic(Type awaitable)
        {
            var argument = awaitable.GenericTypeArguments[0];
            if (argument.ContainsGenericParameters)
                throw NotRegistered(awaitable);

            var open = typeof(AsyncRegistry).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Register) && method.IsGenericMethodDefinition);
            open.MakeGenericMethod(argument).Invoke(null, null);
        }

        static NotSupportedException NotRegistered(Type awaitable)
        {
            var argument = awaitable.IsGenericType ? awaitable.GenericTypeArguments[0] : awaitable;
            return new NotSupportedException(ThisAssembly.Strings.AsyncAdapterNotRegistered(
                CSharpTypeName.Format(awaitable),
                CSharpTypeName.Format(argument)));
        }

        sealed class Adapter
        {
            readonly Func<object, ValueTask<(object? Value, Exception? Error)>> unwrap;
            readonly Func<Task<(object? Value, Exception? Error)>, object> start;

            Adapter(
                Func<object, ValueTask<(object? Value, Exception? Error)>> unwrap,
                Func<Task<(object? Value, Exception? Error)>, object> start)
            {
                this.unwrap = unwrap;
                this.start = start;
            }

            public ValueTask<(object? Value, Exception? Error)> Unwrap(object instance) => unwrap(instance);

            public object Start(Task<(object? Value, Exception? Error)> pending) => start(pending);

            public static Adapter PlainTask() => new(UnwrapTask, StartPlainTask);

            public static Adapter PlainValueTask() => new(UnwrapValueTask, pending => new ValueTask((Task)StartPlainTask(pending)));

            public static Adapter TaskOf<T>() => new(UnwrapTaskOf<T>, StartTask<T>);

            public static Adapter ValueTaskOf<T>() => new(UnwrapValueTaskOf<T>, pending => new ValueTask<T>((Task<T>)StartTask<T>(pending)));

            static async ValueTask<(object? Value, Exception? Error)> UnwrapTask(object instance)
            {
                try
                {
                    await ((Task)instance).ConfigureAwait(false);
                    return (null, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            static async ValueTask<(object? Value, Exception? Error)> UnwrapValueTask(object instance)
            {
                try
                {
                    await ((ValueTask)instance).ConfigureAwait(false);
                    return (null, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            static async ValueTask<(object? Value, Exception? Error)> UnwrapTaskOf<T>(object instance)
            {
                try
                {
                    return (await ((Task<T>)instance).ConfigureAwait(false), null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            static async ValueTask<(object? Value, Exception? Error)> UnwrapValueTaskOf<T>(object instance)
            {
                try
                {
                    return (await ((ValueTask<T>)instance).ConfigureAwait(false), null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            static object StartPlainTask(Task<(object? Value, Exception? Error)> pending)
            {
                if (pending.Status == TaskStatus.RanToCompletion)
                    return Box(pending.Result.Error);

                var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        source.TrySetException(task.Exception!.InnerExceptions);
                    else if (task.IsCanceled)
                        source.TrySetCanceled();
                    else
                        Complete(source, task.Result.Error, false);
                }, TaskScheduler.Default);
                return source.Task;
            }

            static object StartTask<T>(Task<(object? Value, Exception? Error)> pending)
            {
                if (pending.Status == TaskStatus.RanToCompletion)
                    return Box<T>(pending.Result.Value, pending.Result.Error);

                var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        source.TrySetException(task.Exception!.InnerExceptions);
                    else if (task.IsCanceled)
                        source.TrySetCanceled();
                    else
                        Complete(source, task.Result.Value, task.Result.Error);
                }, TaskScheduler.Default);
                return source.Task;
            }

            static object Box(Exception? error)
            {
                if (error is OperationCanceledException canceled)
                    return Task.FromCanceled(canceled.CancellationToken);
                if (error != null)
                    return Task.FromException(error);
                return Task.CompletedTask;
            }

            static object Box<T>(object? value, Exception? error)
            {
                if (error is OperationCanceledException canceled)
                    return Task.FromCanceled<T>(canceled.CancellationToken);
                if (error != null)
                    return Task.FromException<T>(error);
                return Task.FromResult((T)value!);
            }

            static void Complete(TaskCompletionSource<bool> source, Exception? error, bool value)
            {
                if (error is OperationCanceledException canceled)
                {
#if NET8_0_OR_GREATER
                    source.TrySetCanceled(canceled.CancellationToken);
#else
                    source.TrySetCanceled();
#endif
                    return;
                }

                if (error != null)
                    source.TrySetException(error);
                else
                    source.TrySetResult(value);
            }

            static void Complete<T>(TaskCompletionSource<T> source, object? value, Exception? error)
            {
                if (error is OperationCanceledException canceled)
                {
#if NET8_0_OR_GREATER
                    source.TrySetCanceled(canceled.CancellationToken);
#else
                    source.TrySetCanceled();
#endif
                    return;
                }

                if (error != null)
                    source.TrySetException(error);
                else
                    source.TrySetResult((T)value!);
            }
        }
    }
}
