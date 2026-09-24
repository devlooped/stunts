using System.Threading;

namespace Stunts
{
    /// <summary>
    /// Provides the global <see cref="Default"/> and <see cref="LocalDefault"/> 
    /// behavior pipeline factory used when creating new stunts.
    /// </summary>
    /// <remarks>
    /// Stunts will use <see cref="Default"/>.<see cref="IBehaviorPipelineFactory.CreatePipeline{TStunt}"/> 
    /// whenever a new stunt is instantiated, to initialize a behavior pipeline that is invoked in the 
    /// constructor itself, even before further configuration can be performed on the created instance. 
    /// <para>
    /// This is typically only needed for advanced scenarios.
    /// </para>
    /// </remarks>
    public static class BehaviorPipelineFactory
    {
        static readonly AsyncLocal<IBehaviorPipelineFactory?> localFactory = new();
        static IBehaviorPipelineFactory defaultFactory = new DefaultBehaviorPipelineFactory();

        /// <summary>
        /// Gets or sets the global default <see cref="IBehaviorPipelineFactory"/> to use 
        /// for creating the initial pipelines used during a stunt instantiation.
        /// </summary>
        /// <remarks>
        /// A <see cref="LocalDefault"/> can override the value of this global 
        /// default, if assigned to a non-null value.
        /// </remarks>
        public static IBehaviorPipelineFactory Default
        {
            get => localFactory.Value ?? defaultFactory;
            set => defaultFactory = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="IBehaviorPipelineFactory"/> to use 
        /// in the current (async) flow, so it does not affect other threads/flows.
        /// This is typically used in tests to isolate the default pipeline configurations.
        /// </summary>
        public static IBehaviorPipelineFactory? LocalDefault
        {
            get => localFactory.Value;
            set => localFactory.Value = value;
        }

        class DefaultBehaviorPipelineFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline();
        }
    }
}
