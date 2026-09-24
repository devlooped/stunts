using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Stunts
{
    /// <summary>
    /// Interface implemented by all stunts.
    /// </summary>
    // These attributes prevent registering the "Implement through behavior pipeline" codefix.
    // See CustomMockCodeFixProvider and its base class CustomStuntCodeFixProvider.
    [GeneratedCode("Stunts", ThisAssembly.Info.Version)]
    [CompilerGenerated]
    public interface IStunt
    {
        /// <summary>
        /// Behaviors configured for the stunt.
        /// </summary>
        IList<IStuntBehavior> Behaviors { get; }
    }
}