using System;
using System.ComponentModel.DataAnnotations;

namespace Stunts
{
    /// <summary>
    /// Annotates a method that is a factory for stunts, so that a
    /// compile-time or design-time generator can generate them ahead of time.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="ValidationAttribute"/> lets
    /// <c>NativeValidation</c> treat a generic factory as a
    /// validated factory. <c>Stunt.Of&lt;T&gt;()</c> then registers <c>T</c> without a
    /// separate stunt validation generator.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class StuntGeneratorAttribute : ValidationAttribute
    {
    }
}
