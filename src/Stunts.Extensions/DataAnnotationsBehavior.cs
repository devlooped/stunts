using System;
using System.ComponentModel.DataAnnotations;

namespace Stunts
{
    /// <summary>
    /// Rejects a call whose arguments fail the data annotations registered for that member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source generator in this package records every supported validation attribute on
    /// interceptable members and parameters. This behavior looks that registry up before it
    /// calls the rest of the pipeline. A property is checked on its setter. A method or
    /// constructor is checked through its annotated parameters. Getters are left alone.
    /// </para>
    /// <para>
    /// The first failing rule becomes a <see cref="ValidationException"/> and the rest of the
    /// pipeline does not run. Add this behavior after the behaviors that should observe that
    /// rejection, and ahead of the behaviors that should see only valid calls. The pipeline
    /// runs the first behavior outermost, so adding this one last places the check immediately
    /// before the target.
    /// </para>
    /// <para>
    /// <see cref="CompareAttribute"/> reads the other property by calling it on the stunt.
    /// That call re-enters the pipeline. A <see cref="SynchronizedBehavior"/> wrapped outside
    /// this one throws on that re-entry, because the thread already holds the lock.
    /// </para>
    /// <para>
    /// Built-in attributes are implemented by <see cref="DataAnnotationsValidator"/> with the
    /// attribute arguments inlined. The generated code does not instantiate validation
    /// attributes and does not reflect over members. A custom <see cref="ValidationAttribute"/>
    /// subclass is reported and not enforced. <see cref="CustomValidationAttribute"/> is emitted
    /// as a direct call to its static method.
    /// </para>
    /// <para>
    /// Closed generic types are discovered from <see cref="StuntGeneratorAttribute"/> calls,
    /// <c>[assembly: Stunt&lt;T&gt;]</c>, <see cref="ValidatedAttribute"/> factories, and
    /// <c>[assembly: Validate&lt;T&gt;]</c>. A library can mark its own factory with
    /// <see cref="ValidatedAttribute"/> without sharing the stunt generator attribute.
    /// </para>
    /// </remarks>
    public sealed class DataAnnotationsBehavior : IStuntBehavior
    {
        /// <summary>Applies when the invoked member has registered validation rules.</summary>
        public bool AppliesTo(IMethodInvocation invocation) => DataAnnotationsRegistry.AppliesTo(invocation.MethodBase);

        /// <summary>
        /// Validates the registered parameters, then calls <paramref name="next"/>.
        /// A failure returns <see cref="ValidationException"/> without calling <paramref name="next"/>.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            if (invocation == null)
                throw new ArgumentNullException(nameof(invocation));
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            if (DataAnnotationsRegistry.Validate(invocation) is Exception exception)
                return invocation.CreateExceptionReturn(exception);

            return next(invocation, next);
        }
    }
}
