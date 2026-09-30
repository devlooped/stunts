using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    /// <summary>
    /// Castle async interception: the behavior returns a task, and can compose
    /// the task produced by proceed.
    /// </summary>
    public class AsyncTests : IRunnable
    {
        public void Run()
        {
            BehaviorCanReturnATask().GetAwaiter().GetResult();
            BehaviorCanComposeTheProceededTask().GetAwaiter().GetResult();
        }

        public async Task BehaviorCanReturnATask()
        {
            IService stunt = Stunt.For<IService>().AddBehavior((invocation, next) =>
                invocation.MethodBase.Name == nameof(IService.GetAsync)
                    ? invocation.CreateValueReturn(Task.FromResult(7))
                    : invocation.CreateValueReturn(Task.CompletedTask)).ToObject();

            await stunt.DoAsync();

            Assert.Equal(7, await stunt.GetAsync());
        }

        public async Task BehaviorCanComposeTheProceededTask()
        {
            Service stunt = Stunt.For<Service>().AddBehavior((invocation, next) =>
            {
                var proceeded = next(invocation, next);
                var task = (Task<int>)proceeded.ReturnValue;
                return invocation.CreateValueReturn(task.ContinueWith(completed => completed.Result + 1));
            }, invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(4, await stunt.GetAsync());
        }

        public interface IService
        {
            Task DoAsync();

            Task<int> GetAsync();
        }

        public class Service
        {
            public virtual Task<int> GetAsync() => Task.FromResult(3);
        }
    }
}
