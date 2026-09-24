using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Stunts
{
    /// <summary>
    /// Represents the returned value(s) or exception from a method 
    /// invocation.
    /// </summary>
    public record MethodReturn : IMethodReturn
    {
        readonly IMethodInvocation invocation;

        /// <summary>
        /// Initializes a value returning call.
        /// </summary>
        /// <param name="invocation">The invocation that generated the return.</param>
        /// <param name="returnValue">Optional return value for non-void methods.</param>
        /// <param name="arguments">All arguments of the invocation, including ref/out ones.</param>
        public MethodReturn(IMethodInvocation invocation, object? returnValue, IArgumentCollection arguments)
        {
            if (invocation.MethodBase.GetParameters().Length != arguments.Count)
                throw new ArgumentException(ThisAssembly.Strings.MethodArgumentsMismatch(invocation.MethodBase.Name, invocation.MethodBase.GetParameters().Length, arguments.Count), nameof(arguments));

            this.invocation = invocation;

            ReturnValue = returnValue;
            Outputs = GetOutputs(arguments);
        }

        /// <summary>
        /// Initializes a return that represents a thrown exception from the invocation.
        /// </summary>
        /// <param name="invocation">The invocation that generated the return.</param>
        /// <param name="exception">The exception resulting from the invocation.</param>
        public MethodReturn(IMethodInvocation invocation, Exception exception)
        {
            this.invocation = invocation;
            Outputs = GetOutputs(invocation.Arguments);
            Exception = exception;
        }

        /// <inheritdoc />
        public IArgumentCollection Outputs { get; init; }

        /// <inheritdoc />
        public object? ReturnValue { get; init; }

        /// <inheritdoc />
        public Exception? Exception { get; init; }

        /// <inheritdoc />
        public IDictionary<string, object> Context => invocation.Context;

        /// <summary>
        /// Gets a friendly representation of the object.
        /// </summary>
        /// <devdoc>
        /// We don't want to optimize code coverage for this since it's a debugger aid only. 
        /// Annotating this method with DebuggerNonUserCode achieves that.
        /// No actual behavior depends on these strings.
        /// </devdoc>
        [ExcludeFromCodeCoverage]
        [DebuggerNonUserCode]
        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append(invocation.ToString());

            if (Exception != null)
            {
                result.Append($" => throw new {Exception.GetType().Name}(\"{Exception.Message}\")");
            }
            else if (invocation.MethodBase is MethodInfo r && r.ReturnType != typeof(void))
            {
                result.Append(" => ")
                    .Append(
                        ReturnValue == null ? "null" :
                        r.ReturnType == typeof(string) ? $"\"{ReturnValue}\"" :
                        r.ReturnType == typeof(bool) ? ReturnValue.ToString().ToLowerInvariant() :
                        ReturnValue);
            }

            return result.ToString();
        }

        IArgumentCollection GetOutputs(IArgumentCollection arguments)
        {
            var outputs = new ArgumentCollection(
                arguments.Where(x => x.Parameter.ParameterType.IsByRef || x.Parameter.IsOut)
                .ToArray());

            return outputs;
        }
    }
}
