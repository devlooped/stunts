namespace Stunts
{
    /// <summary>
    /// An <see cref="ISyntaxProcessor"/> whose output depends on how the instance was 
    /// configured (e.g. constructor arguments), not only on its type.
    /// </summary>
    /// <remarks>
    /// The generation cache identifies processors by type. A cacheable processor (one of 
    /// Stunts itself, or marked <see cref="SyntaxOnlyProcessorAttribute"/>) with configuration 
    /// that changes the code it emits must implement this interface, or instances configured 
    /// differently share cached stunts.
    /// </remarks>
    public interface IConfiguredSyntaxProcessor : ISyntaxProcessor
    {
        /// <summary>
        /// Describes the configuration that changes the emitted code. Equal values must 
        /// produce equal output for the same syntax.
        /// </summary>
        string Configuration { get; }
    }
}
