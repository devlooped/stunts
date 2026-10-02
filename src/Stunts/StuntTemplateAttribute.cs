using System;
using System.ComponentModel;

namespace Stunts
{
    /// <summary>
    /// Identifies generated metadata describing the target types of a generic stunt template.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StuntTemplateAttribute : Attribute
    {
    }
}
