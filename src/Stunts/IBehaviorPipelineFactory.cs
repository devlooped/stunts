namespace Stunts
{
    /// <summary>
    /// Creates and optionally initializes the <see cref="BehaviorPipeline"/> 
    /// for every stunt instantiation.
    /// </summary>
    public interface IBehaviorPipelineFactory
    {
        /// <summary>
        /// Creates the pipeline for stunts of type <typeparamref name="TStunt"/>.
        /// </summary>
        /// <typeparam name="TStunt">The type of stunt being instantiated.</typeparam>
        /// <returns>The configured pipeline to use.</returns>
        BehaviorPipeline CreatePipeline<TStunt>();
    }
}
