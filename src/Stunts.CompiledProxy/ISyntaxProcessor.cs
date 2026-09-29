using Microsoft.CodeAnalysis;

namespace Stunts
{
    /// <summary>
    /// A processor of <see cref="SyntaxNode"/> that can alter the resulting 
    /// syntax in arbitrary ways.
    /// </summary>
    public interface ISyntaxProcessor
    {
        /// <summary>
        /// Gets the language supported by the processor.
        /// </summary>
        string Language { get; }

        /// <summary>
        /// Gets the phase at which an <see cref="ISyntaxProcessor"/> acts.
        /// </summary>
        ProcessorPhase Phase { get; }

        /// <summary>
        /// Processes the syntax root of the code being generated and optionally 
        /// modifies it.
        /// </summary>
        /// <param name="syntax">The syntax root of the source document.</param>
        /// <param name="context">The processor context.</param>
        SyntaxNode Process(SyntaxNode syntax, ProcessorContext context);
    }
}
