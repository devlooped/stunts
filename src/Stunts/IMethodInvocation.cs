using System;
using System.Collections.Generic;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// Represents a method invocation.
    /// </summary>
    public interface IMethodInvocation : IEquatable<IMethodInvocation>
    {
        /// <summary>
        /// The arguments of the method invocation.
        /// </summary>
        IArgumentCollection Arguments { get; }

        /// <summary>
        /// An arbitrary property bag used during the invocation.
        /// </summary>
        IDictionary<string, object> Context { get; }

        /// <summary>
        /// The runtime method being invoked.
        /// </summary>
        MethodBase MethodBase { get; }

        /// <summary>
        /// The stunt object that was the target of the method invocation.
        /// </summary>
        object Target { get; }

        /// <summary>
        /// Behaviors in the pipeline that should be skipped during this invocation.
        /// </summary>
        HashSet<Type> SkipBehaviors { get; }

        /// <summary>
        /// Whether the method has a built-in implementation that can be invoked via 
        /// <see cref="CreateInvokeReturn"/>.
        /// </summary>
        /// <remarks>
        /// For a class-based stunt, this would be <see langword="true"/> for virtual 
        /// methods, for an interface-based stunt, it would be <see langword="true"/> 
        /// if a target implementation instance was provided (i.e. as a decorator pattern).
        /// </remarks>
        bool HasImplementation { get; }

        /// <summary>
        /// Invokes the underlying implementation of the method, if <see cref="HasImplementation"/> 
        /// is <see langword="true"/>, optionally overriding the arguments passed to that invocation.
        /// </summary>
        /// <param name="arguments">Ordered list of all arguments to the method invocation, including ref/out arguments.
        /// If not provided, the arguments from the invocation will be used.</param>
        /// <returns>The <see cref="IMethodReturn"/> for the current invocation.</returns>
        /// <exception cref="NotImplementedException">The current method invocation does not have a 
        /// base implementation. In other words, it's either abstract or a member of an interface (with 
        /// no target implementation).</exception>
        /// <exception cref="NotSupportedException">The target invocation attempted to get the 
        /// next behavior, which is not supported.</exception>
        IMethodReturn CreateInvokeReturn(IArgumentCollection? arguments = null);

        /// <summary>
        /// Creates the method invocation return that ends the current invocation by providing 
        /// the optional return value (for non-void methods) and optionally the ref/out argument 
        /// values.
        /// </summary>
        /// <param name="returnValue">Optional return value from the method invocation. <see langword="null"/> for <see langword="void"/> methods.</param>
        /// <param name="arguments">Ordered list of all arguments to the method invocation, including ref/out arguments.</param>
        /// <returns>The <see cref="IMethodReturn"/> for the current invocation.</returns>
        IMethodReturn CreateValueReturn(object? returnValue, IArgumentCollection arguments);

        /// <summary>
        /// Creates a method invocation return that represents 
        /// a thrown exception.
        /// </summary>
        /// <param name="exception">The exception to throw from the method invocation.</param>
        /// <returns>The <see cref="IMethodReturn"/> for the current invocation.</returns>
        IMethodReturn CreateExceptionReturn(Exception exception);
    }
}