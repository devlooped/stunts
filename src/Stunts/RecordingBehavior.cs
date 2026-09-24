using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stunts
{
    /// <summary>
    /// Simple behavior that keeps track of all invocations.
    /// </summary>
    [DebuggerDisplay("Count = {Invocations.Count}")]
    public class RecordingBehavior : IStuntBehavior
    {
        /// <summary>
        /// A list of all invocations and their result.
        /// </summary>
        public List<RecordedInvocation> Invocations { get; } = new();

        /// <summary>
        /// Invocation recording applies to all invocations, so it 
        /// always returns <see langword="true"/>.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Invokes the next behavior in the pipeline and records 
        /// the invocation and the result.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var result = next.Invoke(invocation, next);
            Invocations.Add(new RecordedInvocation(invocation, result));
            return result;
        }

        /// <summary>
        /// Returns the friendly rendering of all invocations performed.
        /// </summary>
        public override string ToString() => string.Join(Environment.NewLine, Invocations.Select(i => i.Return.ToString()));

        /// <summary>
        /// A recorded invocation.
        /// </summary>
        public class RecordedInvocation
        {
            /// <summary>
            /// Creates a recorded invocation entry.
            /// </summary>
            public RecordedInvocation(IMethodInvocation invocation, IMethodReturn @return)
                => (Invocation, Return)
                = (invocation, @return);

            /// <summary>
            /// The recorded invocation.
            /// </summary>
            public IMethodInvocation Invocation { get; }

            /// <summary>
            /// The recorded return from the invocation.
            /// </summary>
            public IMethodReturn Return { get; }

            /// <summary>
            /// Provides a friendly rendering of the recorded invocation.
            /// </summary>
            public override string ToString() => Return.ToString();
        }
    }
}
