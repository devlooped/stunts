using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Stunts;

namespace Samples.Memoization
{
    /// <summary>
    /// Remembers the result of a method for each set of argument values, so later calls
    /// with the same arguments skip the rest of the pipeline.
    /// </summary>
    /// <remarks>
    /// The entries are a field of the behavior, so they live and die with the one stunt
    /// whose pipeline holds it. The constructor is internal so that the only way to add it
    /// is <see cref="MemoizeExtensions"/>, which registers a factory: a builder then creates
    /// one memoizer per stunt it builds, instead of sharing one instance (and its entries)
    /// across all of them.
    /// </remarks>
    public sealed class MemoizeBehavior : IStuntBehavior
    {
        readonly ConcurrentDictionary<MethodBase, bool> applies = new();
        readonly ConcurrentDictionary<CallKey, Lazy<IMethodReturn>> entries = new();

        internal MemoizeBehavior() { }

        public bool AppliesTo(IMethodInvocation invocation)
            => applies.GetOrAdd(invocation.MethodBase, IsMemoizable);

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var key = new CallKey(invocation.MethodBase, invocation.Arguments.Select(x => x.RawValue).ToArray());
            // ExecutionAndPublication makes concurrent callers for one key wait for a single
            // execution of the rest of the pipeline and share its result.
            var entry = entries.GetOrAdd(key, _ => new Lazy<IMethodReturn>(
                () => next.Invoke(invocation, next), LazyThreadSafetyMode.ExecutionAndPublication));

            IMethodReturn result;
            try
            {
                result = entry.Value;
            }
            catch
            {
                entries.TryRemove(new KeyValuePair<CallKey, Lazy<IMethodReturn>>(key, entry));
                throw;
            }

            if (result.Exception is { } exception)
            {
                // Failures are not remembered: the next call runs the target again.
                entries.TryRemove(new KeyValuePair<CallKey, Lazy<IMethodReturn>>(key, entry));
                return invocation.CreateExceptionReturn(exception);
            }

            // The stored return belongs to the invocation that ran it, so each caller gets
            // a return of its own with the shared value.
            return invocation.CreateValueReturn(result.ReturnValue, invocation.Arguments);
        }

        static bool IsMemoizable(MethodBase method)
            => method is MethodInfo info &&
               // Property and event accessors read or change state: a setter would not
               // invalidate a remembered getter.
               !info.IsSpecialName &&
               info.ReturnType != typeof(void) &&
               // A ref return or ref struct cannot be stored, and there is no single value
               // to reuse when the method writes back to ref or out arguments.
               !info.ReturnType.IsByRef &&
               !info.ReturnType.IsByRefLike &&
               !info.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsByRefLike) &&
               // A Task or ValueTask would be remembered even when it faults later, and a
               // ValueTask can only be awaited once: storing their unwrapped results is the
               // job of the cache behavior of devlooped/stunts#267, not of this sample.
               !IsAwaitable(info.ReturnType);

        static bool IsAwaitable(Type type)
            => typeof(Task).IsAssignableFrom(type) ||
               type == typeof(ValueTask) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>));

        /// <summary>
        /// The method and its argument values, compared with their default equality.
        /// </summary>
        sealed class CallKey : IEquatable<CallKey>
        {
            readonly MethodBase method;
            readonly object?[] arguments;
            readonly int hashCode;

            public CallKey(MethodBase method, object?[] arguments)
            {
                this.method = method;
                this.arguments = arguments;

                var hash = new HashCode();
                hash.Add(method);
                foreach (var argument in arguments)
                    hash.Add(argument);

                hashCode = hash.ToHashCode();
            }

            public bool Equals(CallKey? other)
                => other is not null &&
                   method.Equals(other.method) &&
                   arguments.SequenceEqual(other.arguments);

            public override bool Equals(object? obj) => Equals(obj as CallKey);

            public override int GetHashCode() => hashCode;
        }
    }
}
