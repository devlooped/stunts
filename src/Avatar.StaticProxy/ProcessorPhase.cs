using System;

namespace Avatars
{
    /// <summary>
    /// The phase at which an <see cref="IDocumentProcessor"/> acts.
    /// </summary>
    public enum ProcessorPhase
    {
        /// <summary>
        /// Initial avatar generation phase where the basic imports, namespace and 
        /// blank avatar class declaration and initial base type and implemented interfaces 
        /// are laid out in the doc. This is typically the phase were additional interfaces 
        /// can be injected to participate in the subsequent phases automatically. 
        /// </summary>
        /// <remarks>
        /// Moq's <c>IMocked</c> interface is registered in this phase, for example.
        /// </remarks>
        Prepare,

        /// <summary>
        /// Phase that generates constructors and stub implementations for abstract, virtual,
        /// and interface members. Virtual stubs call <c>base</c>. Abstract and interface stubs
        /// throw <see cref="NotImplementedException"/>.
        /// </summary>
        Scaffold,

        /// <summary>
        /// Executed right after scaffold, this phase performs the initial avatar implementation rewriting 
        /// by replacing methods that throw <see cref="NotImplementedException"/> generated during scaffold 
        /// and invokes the <see cref="BehaviorPipeline"/> instead for each of them.
        /// </summary>
        Rewrite,

        /// <summary>
        /// Final phase that allows generators to perform additional generation beyond scaffold 
        /// and initial avatar rewriting. Members generated in this phase are not rewritten at all
        /// to use the <see cref="BehaviorPipeline"/> and can consist of language-specific fixups 
        /// or cleanups to make the generated code more idiomatic.
        /// </summary>
        Fixup,
    }
}
