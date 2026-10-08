using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Stunts
{
    /// <summary>
    /// Translates an exception from the rest of the pipeline.
    /// </summary>
    /// <remarks>
    /// The behavior observes the call through <c>Proceed</c>. A synchronous failure is thrown
    /// by the stunt. A faulted <see cref="Task"/> or <see cref="ValueTask"/> stays a fault of
    /// the returned awaitable, and <see cref="IMethodReturn.Exception"/> stays <see langword="null"/>.
    /// <para>
    /// The map receives that exception. A <see langword="null"/> result leaves the exception
    /// unchanged. Any other instance replaces it. A replacement that wraps the original keeps
    /// that original as its <see cref="Exception.InnerException"/>.
    /// </para>
    /// <para>
    /// Pass <c>swallow: true</c> to let a <see langword="null"/> result complete the call with
    /// the value from <see cref="DefaultValueProvider"/>. The switch defaults to off, so a null
    /// result cannot turn a failure into a value. Native AOT uses the typed factories the stunt
    /// already registers.
    /// </para>
    /// <para>
    /// Behaviors added before this one sit outside the translated call. Exceptions they throw
    /// are left alone.
    /// </para>
    /// </remarks>
    public sealed class ExceptionMappingBehavior : IStuntBehavior
    {
        static readonly DefaultValueProvider defaults = new();

        readonly Func<Exception, Exception?> map;
        readonly bool swallow;

        /// <summary>
        /// Initializes the behavior.
        /// </summary>
        /// <param name="map">
        /// Translates the failure. Return <see langword="null"/> to leave the exception unchanged,
        /// or another instance to replace it. When <paramref name="swallow"/> is <see langword="true"/>,
        /// <see langword="null"/> completes the call with the <see cref="DefaultValueProvider"/> value.
        /// </param>
        /// <param name="swallow">
        /// When <see langword="true"/>, a <see langword="null"/> map result turns the failure into
        /// the <see cref="DefaultValueProvider"/> value. Defaults to <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
        public ExceptionMappingBehavior(Func<Exception, Exception?> map, bool swallow = false)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.swallow = swallow;
        }

        /// <summary>Translates every invocation the pipeline dispatches.</summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Runs <paramref name="next"/> through <c>Proceed</c> and applies <c>map</c> to a failure.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
            => invocation.Proceed(next, (outcome, again) =>
            {
                if (outcome.Exception == null)
                    return new ValueTask<ProceedOutcome>(outcome);

                var translated = map(outcome.Exception);
                if (translated == null)
                {
                    if (!swallow)
                        return new ValueTask<ProceedOutcome>(outcome);

                    return new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue(Default(invocation), outcome.Elapsed));
                }

                if (ReferenceEquals(translated, outcome.Exception))
                    return new ValueTask<ProceedOutcome>(outcome);

                return new ValueTask<ProceedOutcome>(ProceedOutcome.FromException(translated, outcome.Elapsed));
            });

        static object? Default(IMethodInvocation invocation)
        {
            var type = ResultType(invocation);
            return type == null ? null : defaults.GetDefault(type);
        }

        static Type? ResultType(IMethodInvocation invocation)
        {
            if (invocation.MethodBase is not MethodInfo info || info.ReturnType == typeof(void))
                return null;

            var awaitable = AsyncRegistry.AwaitableReturn(info);
            if (awaitable == null)
                return info.ReturnType;
            if (!awaitable.IsGenericType)
                return null;

            return awaitable.GenericTypeArguments[0];
        }
    }
}
