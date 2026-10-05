using System;
using System.Threading.Tasks;
using Xunit;

namespace Stunts.UnitTests.Castle
{
    public class ProceedClassTests : IRunnable
    {
        public void Run()
        {
            BaseExceptionIsTheOutcome();
            BaseTaskCanBeAdjusted().GetAwaiter().GetResult();
        }

        public void BaseExceptionIsTheOutcome()
        {
            Calculator stunt = Stunt.For<Calculator>().AddBehavior(
                (invocation, next) => invocation.Proceed(next, (outcome, again) => new ValueTask<ProceedOutcome>(outcome)),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var exception = Assert.Throws<InvalidOperationException>(() => stunt.Read());
            Assert.Equal("base", exception.Message);
        }

        public async Task BaseTaskCanBeAdjusted()
        {
            Calculator stunt = Stunt.For<Calculator>().AddBehavior(
                (invocation, next) => invocation.Proceed(next, (outcome, again) =>
                    new ValueTask<ProceedOutcome>(ProceedOutcome.FromValue((int)outcome.Value! + 1, outcome.Elapsed))),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            Assert.Equal(4, await stunt.GetAsync());
        }

        public class Calculator
        {
            public virtual int Read() => throw new InvalidOperationException("base");

            public virtual Task<int> GetAsync() => Task.FromResult(3);
        }
    }
}
