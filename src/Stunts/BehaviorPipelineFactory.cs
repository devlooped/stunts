using System;
using System.Threading;

namespace Stunts
{
    /// <summary>
    /// Provides the global <see cref="Default"/> behavior pipeline factory used when creating new stunts.
    /// </summary>
    /// <remarks>
    /// Stunts will use <see cref="Default"/>.<see cref="IBehaviorPipelineFactory.CreatePipeline{TStunt}"/> 
    /// whenever a new stunt is instantiated, to initialize a behavior pipeline that is invoked in the 
    /// constructor itself, even before further configuration can be performed on the created instance. 
    /// <para>
    /// This is typically only needed for advanced scenarios. For testing, an ambient <see cref="IBehaviorPipelineFactory"/> 
    /// can be set via <see cref="UseAmbient(IBehaviorPipelineFactory)"/> to override the global default for the 
    /// current execution context.
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
        /// An ambient <see cref="IBehaviorPipelineFactory"/> can override the value of this global 
        /// default, via <see cref="UseAmbient(IBehaviorPipelineFactory)"/>. This is typically only needed for testing.
        /// </remarks>
        public static IBehaviorPipelineFactory Default
        {
            get => localFactory.Value ?? defaultFactory;
            set => defaultFactory = value;
        }

        /// <summary>
        /// Sets an ambient <see cref="IBehaviorPipelineFactory"/> to use for <see cref="Default"/> for the current execution context.
        /// </summary>
        /// <param name="factory">The <see cref="IBehaviorPipelineFactory"/> to set as the ambient <see cref="Default"/>.</param>
        /// <returns>An <see cref="IDisposable"/> that, when disposed, restores the original execution context default.</returns>
        public static IDisposable UseAmbient(IBehaviorPipelineFactory factory) => new AmbientDisposable(localFactory, factory);

        class AmbientDisposable : IDisposable
        {
            readonly AsyncLocal<IBehaviorPipelineFactory?> storage;
            readonly IBehaviorPipelineFactory? original;

            public AmbientDisposable(AsyncLocal<IBehaviorPipelineFactory?> storage, IBehaviorPipelineFactory factory)
            {
                this.storage = storage;
                original = storage.Value;
                storage.Value = factory;
            }
            public void Dispose() => storage.Value = original;
        }

        class DefaultBehaviorPipelineFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline();
        }
    }
}
