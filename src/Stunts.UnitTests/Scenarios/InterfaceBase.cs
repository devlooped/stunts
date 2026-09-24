#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using Xunit;

namespace Stunts.Scenario.InterfaceBase
{
    public interface IBasicInterface
    {
        void Run();
    }

    /// <summary>
    /// Basic interface implementation and behaviors are correct.
    /// </summary>
    public class Test : IRunnable
    {
        public void Run()
        {
            BehaviorPipelineFactory.LocalDefault = new RecordingBehaviorPipelineFactory();
            var stunt = Stunt.Of<IBasicInterface>();

            Assert.NotNull(stunt);
            Assert.IsAssignableFrom<IStunt>(stunt);

            // Recorder tracks call to constructor.
            Assert.Single(((IStunt)stunt).Behaviors);
            Assert.Single(((RecordingBehavior)((IStunt)stunt).Behaviors[0]).Invocations);

            // If no returning behavior is configured, invoking it throws.
            Assert.Throws<NotImplementedException>(() => stunt.Run());

            // When we add at least one matching behavior, invocations succeed.
            stunt.AddBehavior(new DefaultValueBehavior());
            stunt.Run();
        }

        class RecordingBehaviorPipelineFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline(new RecordingBehavior());
        }
    }
}
