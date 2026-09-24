using System.Diagnostics;

namespace Stunts
{
    /// <summary>
    /// A general purpose <see cref="IStuntBehavior"/> that invokes the provided 
    /// handlers for the <see cref="IStuntBehavior.Execute"/> (via a <see cref="ExecuteHandler"/>) 
    /// and <see cref="IStuntBehavior.AppliesTo"/> (via a <see cref="AppliesToHandler"/>) interface methods.
    /// </summary>
    class AnonymousBehavior : IStuntBehavior
    {
        readonly AppliesToHandler appliesTo;
        readonly ExecuteHandler behavior;
        readonly string? name;

        /// <summary>
        /// Creates the behavior from the given delegates.
        /// </summary>
        public AnonymousBehavior(ExecuteHandler behavior, AppliesToHandler? appliesTo = null, string? name = null)
        {
            this.behavior = behavior;
            this.appliesTo = appliesTo ?? new AppliesToHandler(invocation => true);
            this.name = name;
        }

        /// <summary>
        /// Executes the provided <see cref="AppliesToHandler"/> provided in the constructor.
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation) => appliesTo(invocation);

        /// <summary>
        /// Executes the provided <see cref="ExecuteHandler"/> provided in the constructor.
        /// </summary>
        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next) => behavior(invocation, next);

        /// <summary>
        /// Gets a friendly representation of the object.
        /// </summary>
        /// <devdoc>
        /// We don't want to optimize code coverage for this since it's a debugger aid only. 
        /// Annotating this method with DebuggerNonUserCode achieves that.
        /// No actual behavior depends on these strings.
        /// </devdoc>
        [DebuggerNonUserCode]
        public override string ToString() => name ?? "<anonymous>";
    }
}
