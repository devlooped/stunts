using System;
using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// Describes the stunt types an externally visible generic factory method creates, 
    /// so referencing projects can generate them when they call it with concrete types.
    /// The parameter types of the annotated method are the stunt types, expressed in 
    /// terms of the factory method type parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StuntDefinitionAttribute : Attribute
    {
        /// <summary>Initializes the shape for the given factory method.</summary>
        /// <param name="method">Documentation comment identifier of the factory method.</param>
        /// <param name="assembly">Name of the assembly that requests the stunt at run time.</param>
        public StuntDefinitionAttribute(string method, string assembly)
            => (Method, Assembly) = (method, assembly);

        /// <summary>Gets the documentation comment identifier of the factory method.</summary>
        public string Method { get; }

        /// <summary>Gets the name of the assembly that requests the stunt at run time.</summary>
        public string Assembly { get; }
    }
}
