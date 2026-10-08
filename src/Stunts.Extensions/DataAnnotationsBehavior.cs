using System;
using System.ComponentModel.DataAnnotations;
using Devlooped.DataAnnotations;

namespace Stunts
{
    /// <summary>
    /// Rejects a call whose arguments fail the data annotations registered for that member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="NativeValidator"/> and its source generator own the checks. This behavior
    /// looks the registration up before the rest of the pipeline. A property is checked on
    /// its setter. A method or constructor is checked through its annotated parameters.
    /// Getters are left alone. The first failure is a <see cref="ValidationException"/>,
    /// and the rest of the pipeline does not run.
    /// </para>
    /// <para>
    /// Add this behavior after the behaviors that should observe the rejection, and ahead of
    /// the behaviors that should see only valid calls. The pipeline runs the first behavior
    /// outermost, so adding this one last places the check immediately before the target.
    /// </para>
    /// <para>
    /// <see cref="CompareAttribute"/> reads the other property from the instance. On a stunt
    /// that call re-enters the pipeline. A <see cref="SynchronizedBehavior"/> wrapped outside
    /// this one throws on that re-entry, because the thread already holds the lock.
    /// </para>
    /// <para>
    /// <see cref="StuntGeneratorAttribute"/> derives from <see cref="ValidationAttribute"/>, so
    /// the generator treats a generic <c>[StuntGenerator]</c> method as a validated factory and
    /// registers the closed types that call names. A factory that is not a stunt generator uses
    /// <see cref="ValidatedAttribute"/>. A closed type with no factory call uses
    /// <c>[assembly: Validate&lt;T&gt;]</c>.
    /// </para>
    /// </remarks>
    public sealed class DataAnnotationsBehavior : IStuntBehavior
    {
        /// <summary>Applies when the invoked member has registered validation rules.</summary>
        public bool AppliesTo(IMethodInvocation invocation) =>
            invocation != null && NativeValidation.AppliesTo(invocation.MethodBase);

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

            var count = invocation.Arguments.Count;
            var arguments = new object?[count];
            for (var i = 0; i < count; i++)
                arguments[i] = invocation.Arguments.GetValue(i);

            try
            {
                NativeValidator.Validate(invocation.Target, invocation.MethodBase, arguments);
            }
            catch (ValidationException exception)
            {
                return invocation.CreateExceptionReturn(exception);
            }

            return next(invocation, next);
        }
    }
}
