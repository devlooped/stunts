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
            using var ambient = BehaviorPipelineFactory.UseAmbient(new RecordingBehaviorPipelineFactory());
            var stunt = Stunt.For<IBasicInterface>();
            IBasicInterface basic = stunt.ToObject();

            Assert.NotNull(basic);
            Assert.IsAssignableFrom<IStunt>(basic);

            // Recorder tracks call to constructor.
            Assert.Single(stunt.Behaviors);
            Assert.Single(((RecordingBehavior)stunt.Behaviors[0]).Invocations);

            // If no returning behavior is configured, invoking it throws.
            Assert.Throws<NotImplementedException>(() => basic.Run());

            // When we add at least one matching behavior, invocations succeed.
            stunt.AddBehavior(new DefaultValueBehavior());
            basic.Run();
        }

        class RecordingBehaviorPipelineFactory : IBehaviorPipelineFactory
        {
            public BehaviorPipeline CreatePipeline<TStunt>() => new BehaviorPipeline(new RecordingBehavior());
        }
    }
}
