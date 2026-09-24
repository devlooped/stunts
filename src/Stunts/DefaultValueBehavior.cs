using System.Linq;
using System.Reflection;

namespace Stunts
{
    /// <summary>
    /// A <see cref="IStuntBehavior"/> that returns default values from an 
    /// invocation, both for the method return type as well as any out/ref 
    /// parameters.
    /// </summary>
    public class DefaultValueBehavior : IStuntBehavior
    {
        /// <summary>
        /// Initializes the behavior with a default <see cref="DefaultValueProvider"/>.
        /// </summary>
        public DefaultValueBehavior()
            : this(new DefaultValueProvider()) { }

        /// <summary>
        /// Initializes the behavior with a specific <see cref="DefaultValueProvider"/>.
        /// </summary>
        public DefaultValueBehavior(DefaultValueProvider provider) => Provider = provider;

        /// <summary>
        /// Gets or sets the provider of default values for the behavior.
        /// </summary>
        public DefaultValueProvider Provider { get; set; }

        /// <summary>
        /// Always returns <see langword="true" />
        /// </summary>
        public bool AppliesTo(IMethodInvocation invocation) => true;

        /// <summary>
        /// Fills in the ref, out and return values with the defaults determined 
        /// by the <see cref="DefaultValueProvider"/> utility class.
        /// </summary>
        IMethodReturn IStuntBehavior.Execute(IMethodInvocation invocation, ExecuteHandler next)
            => invocation.CreateValueReturn(
                invocation.MethodBase is MethodInfo info && info.ReturnType != typeof(void)
                    ? Provider.GetDefault(info.ReturnType)
                    : default,
                new ArgumentCollection(
                    invocation.Arguments.Select(arg => arg.Parameter.IsOut
                        // Only provide default values for out parameters. 
                        // NOTE: does not touch ByRef values.
                        ? arg.WithRawValue(Provider.GetDefault(arg.Parameter.ParameterType))
                        : arg).ToArray()));
    }
}