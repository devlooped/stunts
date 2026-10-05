using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// The settled result of one attempt performed by <see cref="ProceedExtension.Proceed"/>.
    /// </summary>
    public readonly struct ProceedOutcome
    {
        /// <summary>
        /// Creates a successful attempt. For <see cref="Task{TResult}"/> and <see cref="ValueTask{TResult}"/>,
        /// <paramref name="value"/> is the result, not the awaitable.
        /// </summary>
        public static ProceedOutcome FromValue(object? value, TimeSpan elapsed = default)
            => new(value, null, elapsed, null);

        /// <summary>
        /// Creates a failed attempt. Returning it from the callback fails the invocation.
        /// </summary>
        public static ProceedOutcome FromException(Exception exception, TimeSpan elapsed = default)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return new ProceedOutcome(null, exception, elapsed, null);
        }

        internal static ProceedOutcome Success(object? value, TimeSpan elapsed, IArgumentCollection arguments)
            => new(value, null, elapsed, arguments);

        internal static ProceedOutcome Failure(Exception exception, TimeSpan elapsed, IArgumentCollection arguments)
            => new(null, exception, elapsed, arguments);

        ProceedOutcome(object? value, Exception? exception, TimeSpan elapsed, IArgumentCollection? arguments)
        {
            Value = value;
            Exception = exception;
            Elapsed = elapsed;
            Arguments = arguments;
        }

        /// <summary>The attempt's return value when <see cref="Exception"/> is <see langword="null"/>.</summary>
        public object? Value { get; }

        /// <summary>The attempt's failure, or <see langword="null"/> when it succeeded.</summary>
        public Exception? Exception { get; }

        /// <summary>How long the attempt ran, including the time spent awaiting its task.</summary>
        public TimeSpan Elapsed { get; }

        internal IArgumentCollection? Arguments { get; }
    }

    /// <summary>Invokes the rest of the pipeline once and returns that attempt.</summary>
    public delegate ValueTask<ProceedOutcome> ProceedAgain();

    /// <summary>
    /// Receives one settled attempt and returns the attempt the caller should observe.
    /// </summary>
    public delegate ValueTask<ProceedOutcome> ProceedHandler(ProceedOutcome outcome, ProceedAgain again);

    /// <summary>
    /// Awaits task-returning members before the behavior inspects the result.
    /// </summary>
    public static class ProceedExtension
    {
        /// <summary>
        /// Calls <paramref name="next"/> and passes the settled attempt to <paramref name="callback"/>.
        /// Synchronous members run the callback before this method returns. Task and value-task members
        /// return a new awaitable of the same type immediately; the callback runs when that attempt finishes.
        /// </summary>
        /// <remarks>
        /// The <paramref name="callback"/> receives an again delegate that calls <paramref name="next"/> once more
        /// and waits for that attempt. A synchronous member's callback must itself finish synchronously.
        /// Returning an exception fails the call. A synchronous failure is thrown by the stunt.
        /// A faulted task stays faulted, with the original exception, instead of throwing before a task is returned.
        /// </remarks>
        public static IMethodReturn Proceed(this IMethodInvocation invocation, ExecuteHandler next, ProceedHandler callback)
        {
            if (invocation == null)
                throw new ArgumentNullException(nameof(invocation));
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            return new ProceedCall(invocation, next, callback).Execute();
        }

        sealed class ProceedCall
        {
            readonly IMethodInvocation invocation;
            readonly ExecuteHandler next;
            readonly ProceedHandler callback;
            IArgumentCollection arguments;

            public ProceedCall(IMethodInvocation invocation, ExecuteHandler next, ProceedHandler callback)
            {
                this.invocation = invocation;
                this.next = next;
                this.callback = callback;
                arguments = invocation.Arguments;
            }

            public IMethodReturn Execute()
            {
                var started = Stopwatch.GetTimestamp();
                var result = InvokeNext();
                arguments = ArgumentsOf(result);
                var awaitable = AsyncRegistry.AwaitableReturn(invocation.MethodBase);

                if (result.Exception != null || (awaitable != null && result.ReturnValue is null))
                {
                    var error = result.Exception ?? new NullReferenceException(ThisAssembly.Strings.NullAwaitable(invocation.MethodBase.Name));
                    return Decide(ProceedOutcome.Failure(error, Elapsed(started), arguments), awaitable, synchronousFailure: true);
                }

                if (awaitable == null)
                    return Decide(ProceedOutcome.Success(result.ReturnValue, Elapsed(started), arguments), null, synchronousFailure: true);

                return invocation.CreateValueReturn(
                    AsyncRegistry.Start(awaitable, FinishAsync(result, started, awaitable)),
                    invocation.Arguments);
            }

            IMethodReturn Decide(ProceedOutcome outcome, Type? awaitable, bool synchronousFailure)
            {
                var pending = callback(outcome, Again);
                if (!pending.IsCompleted)
                {
                    if (awaitable == null)
                    {
                        Abandon(pending);
                        throw new InvalidOperationException(ThisAssembly.Strings.ProceedCallbackMustNotAwait(invocation.MethodBase.Name));
                    }

                    return invocation.CreateValueReturn(AsyncRegistry.Start(awaitable, Decision(pending)), invocation.Arguments);
                }

                var decided = pending.GetAwaiter().GetResult();
                var used = decided.Arguments ?? arguments;
                if (decided.Exception != null && (synchronousFailure || awaitable == null))
                    return invocation.CreateExceptionReturn(decided.Exception);
                if (awaitable == null)
                    return invocation.CreateValueReturn(decided.Value, used);

                return invocation.CreateValueReturn(AsyncRegistry.FromOutcome(awaitable, decided), invocation.Arguments);
            }

            async Task<(object? Value, Exception? Error)> FinishAsync(IMethodReturn result, long started, Type awaitable)
            {
                var (value, error) = await AsyncRegistry.Unwrap(awaitable, result.ReturnValue!).ConfigureAwait(false);
                var outcome = error != null
                    ? ProceedOutcome.Failure(error, Elapsed(started), arguments)
                    : ProceedOutcome.Success(value, Elapsed(started), arguments);
                try
                {
                    var decided = await callback(outcome, Again).ConfigureAwait(false);
                    if (decided.Arguments != null)
                        arguments = decided.Arguments;
                    return (decided.Value, decided.Exception);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            ValueTask<ProceedOutcome> Again()
            {
                var started = Stopwatch.GetTimestamp();
                var result = InvokeNext();
                var used = ArgumentsOf(result);
                arguments = used;
                var awaitable = AsyncRegistry.AwaitableReturn(invocation.MethodBase);
                if (result.Exception != null)
                    return new ValueTask<ProceedOutcome>(ProceedOutcome.Failure(result.Exception, Elapsed(started), used));
                if (awaitable == null)
                    return new ValueTask<ProceedOutcome>(ProceedOutcome.Success(result.ReturnValue, Elapsed(started), used));
                if (result.ReturnValue is null)
                    return new ValueTask<ProceedOutcome>(ProceedOutcome.Failure(
                        new NullReferenceException(ThisAssembly.Strings.NullAwaitable(invocation.MethodBase.Name)),
                        Elapsed(started),
                        used));

                return AgainAsync(result, started, awaitable, used);
            }

            async ValueTask<ProceedOutcome> AgainAsync(IMethodReturn result, long started, Type awaitable, IArgumentCollection used)
            {
                var (value, error) = await AsyncRegistry.Unwrap(awaitable, result.ReturnValue!).ConfigureAwait(false);
                return error != null
                    ? ProceedOutcome.Failure(error, Elapsed(started), used)
                    : ProceedOutcome.Success(value, Elapsed(started), used);
            }

            IMethodReturn InvokeNext()
            {
                try
                {
                    return next(invocation, next);
                }
                catch (Exception exception)
                {
                    return invocation.CreateExceptionReturn(exception);
                }
            }

            IArgumentCollection ArgumentsOf(IMethodReturn result)
            {
                var merged = invocation.Arguments;
                foreach (Argument output in result.Outputs)
                    merged = merged.SetValue(output.Name, output.RawValue);
                return merged;
            }
        }

        static async Task<(object? Value, Exception? Error)> Decision(ValueTask<ProceedOutcome> pending)
        {
            var decided = await pending.ConfigureAwait(false);
            return (decided.Value, decided.Exception);
        }

        static void Abandon(ValueTask<ProceedOutcome> pending)
            => _ = pending.AsTask().ContinueWith(
                task => _ = task.Exception,
                default,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        static TimeSpan Elapsed(long started)
            => TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - started) * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
    }
}
