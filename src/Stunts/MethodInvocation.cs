using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TypeNameFormatter;

namespace Stunts
{
    /// <summary>
    /// Default implementation of <see cref="IMethodInvocation"/>.
    /// </summary>
    public partial class MethodInvocation : IEquatable<MethodInvocation>, IMethodInvocation
    {
        readonly ExecuteHandler implementation;

        /// <summary>
        /// Initializes the <see cref="MethodInvocation"/> for a method that has no parameters.
        /// </summary>
        /// <param name="target">The target object where the invocation is being performed.</param>
        /// <param name="method">The method being invoked.</param>
        public MethodInvocation(object target, MethodBase method)
            : this(target, method, new ArgumentCollection())
        {
        }

        /// <summary>
        /// Initializes the <see cref="MethodInvocation"/> with the given parameters.
        /// </summary>
        /// <param name="target">The target object where the invocation is being performed.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="arguments">The arguments of the method invocation.</param>
        public MethodInvocation(object target, MethodBase method, IArgumentCollection arguments)
        {
            // TODO: validate that arguments length and type match the method info?
            Target = target ?? throw new ArgumentNullException(nameof(target));
            MethodBase = method ?? throw new ArgumentNullException(nameof(method));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

            if (method.GetParameters().Length != Arguments.Count)
                throw new TargetParameterCountException(ThisAssembly.Strings.MethodArgumentsMismatch(method.Name, method.GetParameters().Length, Arguments.Count));

            implementation = (m, n) => throw new NotImplementedException(ThisAssembly.Strings.NotImplemented(ToString()));
        }

        /// <summary>
        /// Initializes the <see cref="MethodInvocation"/> with the given parameters.
        /// </summary>
        /// <param name="target">The target object where the invocation is being performed.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="implementation">Delegate to invoke the method implementation.</param>
        public MethodInvocation(object target, MethodBase method, ExecuteHandler implementation)
            : this(target, method, implementation, new ArgumentCollection())
        {
        }

        /// <summary>
        /// Initializes the <see cref="MethodInvocation"/> with the given parameters.
        /// </summary>
        /// <param name="target">The target object where the invocation is being performed.</param>
        /// <param name="method">The method being invoked.</param>
        /// <param name="implementation">Delegate to invoke the method implementation.</param>
        /// <param name="arguments">The arguments of the method invocation.</param>
        public MethodInvocation(object target, MethodBase method, ExecuteHandler implementation, IArgumentCollection arguments)
        {
            // TODO: validate that arguments length and type match the method info?
            Target = target ?? throw new ArgumentNullException(nameof(target));
            MethodBase = method ?? throw new ArgumentNullException(nameof(method));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

            if (method.GetParameters().Length != Arguments.Count)
                throw new TargetParameterCountException(ThisAssembly.Strings.MethodArgumentsMismatch(method.Name, method.GetParameters().Length, Arguments.Count));

            this.implementation = implementation;
            HasImplementation = true;
        }

        /// <inheritdoc />
        public IArgumentCollection Arguments { get; }

        /// <inheritdoc />
        public IDictionary<string, object> Context { get; } = new Dictionary<string, object>();

        /// <inheritdoc />
        public MethodBase MethodBase { get; }

        /// <inheritdoc />
        public object Target { get; }

        /// <inheritdoc />
        public HashSet<Type> SkipBehaviors { get; } = new HashSet<Type>();

        /// <inheritdoc />
        public bool HasImplementation { get; }

        /// <inheritdoc />
        public IMethodReturn CreateExceptionReturn(Exception exception)
            => new MethodReturn(this, exception);

        /// <inheritdoc />
        public IMethodReturn CreateValueReturn(object? returnValue, IArgumentCollection arguments)
            => new MethodReturn(this, returnValue, arguments ?? Arguments);

        /// <inheritdoc />
        public IMethodReturn CreateInvokeReturn(IArgumentCollection? arguments = null)
            => implementation.Invoke(arguments == null ? this : new MethodInvocation(Target, MethodBase, arguments),
                (m, n) => throw new NotSupportedException(ThisAssembly.Strings.GetNextNotSupported));

        /// <summary>
        /// Gets a friendly representation of the invocation.
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
            if (MethodBase is MethodInfo info)
            {
                if (info.ReturnType != typeof(void))
                    result.AppendFormattedName(info.ReturnType).Append(" ");
                else
                    result.Append("void ");
            }

            var isevent = false;

            if (MethodBase.IsSpecialName)
            {
                if (MethodBase.Name.Equals("get_Item", StringComparison.Ordinal) ||
                    MethodBase.Name.Equals("set_Item", StringComparison.Ordinal))
                {
                    result.Append("this");
                }
                else if (MethodBase.Name.StartsWith("get_", StringComparison.Ordinal))
                {
                    result.Append(MethodBase.Name.Substring(4));
                }
                else if (MethodBase.Name.StartsWith("set_", StringComparison.Ordinal))
                {
                    result.Append(MethodBase.Name.Substring(4));
                    result.Append(" = ").Append(Arguments.GetValue(0)?.ToString() ?? "null");
                }
                else if (MethodBase.Name.StartsWith("add_", StringComparison.Ordinal))
                {
                    isevent = true;
                    result.Append(MethodBase.Name.Substring(4) + " += ");
                }
                else if (MethodBase.Name.StartsWith("remove_", StringComparison.Ordinal))
                {
                    isevent = true;
                    result.Append(MethodBase.Name.Substring(7) + " -= ");
                }
            }
            else
            {
                result.Append(MethodBase.Name);
            }

            if (MethodBase.IsGenericMethod)
            {
                var generic = ((MethodInfo)MethodBase).GetGenericMethodDefinition();
                result
                    .Append("<")
                    .Append(string.Join(", ", generic.GetGenericArguments().Select(TypeName)))
                    .Append(">");
            }

            // TODO: render indexer arguments?
            if (!MethodBase.IsSpecialName)
            {
                return result
                    .Append("(")
                    .Append(Arguments.ToString())
                    .Append(")")
                    .ToString();
            }
            else if (MethodBase.Name == "get_Item" || MethodBase.Name == "set_Item")
            {
                return result
                    .Append("[")
                    .Append(Arguments.ToString())
                    .Append("]")
                    .ToString();
            }
            else if (isevent)
            {
                return result
                    .Append(((Delegate)Arguments.GetValue(0)!).GetMethodInfo().Name)
                    .ToString();
            }

            return result.ToString();
        }

        [DebuggerNonUserCode]
        [ExcludeFromCodeCoverage]
        string TypeName(Type type) => type.Name;

        #region Equality

        /// <summary>
        /// Tests the current invocation against another for equality, taking into account the target object 
        /// for reference equality, the object equality of both <see cref="MethodBase"/> and the sequence and 
        /// equality for all <see cref="Arguments"/>.
        /// </summary>
        /// <param name="other">The invocation to compare against.</param>
        /// <returns><see langword="true"/> if the invocations are equal, <see langword="false"/> otherwise.</returns>
        public bool Equals(IMethodInvocation? other)
            => other != null && ReferenceEquals(Target, other.Target) && MethodBase.Equals(other.MethodBase) && Arguments.Equals(other.Arguments);

        /// <summary>
        /// Tests the current invocation against another for equality, taking into account the target object 
        /// for reference equality, the object equality of both <see cref="MethodBase"/> and the sequence and 
        /// equality for all <see cref="Arguments"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the invocations are equal, <see langword="false"/> otherwise.</returns>
        public bool Equals(MethodInvocation other)
            => Equals((IMethodInvocation)other);

        /// <summary>
        /// Tests the current invocation against another for equality, taking into account the target object 
        /// for reference equality, the object equality of both <see cref="MethodBase"/> and the sequence and 
        /// equality for all <see cref="Arguments"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the invocations are equal, <see langword="false"/> otherwise.</returns>
        public override bool Equals(object obj)
            => Equals(obj as IMethodInvocation);

        /// <summary>
        /// Gets the hash code for the current invocation, including the <see cref="Target"/>, <see cref="MethodBase"/> 
        /// and <see cref="Arguments"/>.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(RuntimeHelpers.GetHashCode(Target));
            hash.Add(MethodBase);
            hash.Add(Arguments);
            return hash.ToHashCode();
        }

        #endregion
    }
}