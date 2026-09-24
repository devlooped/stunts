using Stunts;
using Xunit;
using Xunit.Abstractions;

namespace Samples
{
    public class OutputHelperLoggingBehavior : IStuntBehavior
    {
        readonly ITestOutputHelper output;

        public OutputHelperLoggingBehavior(ITestOutputHelper output) => this.output = output;

        public bool AppliesTo(IMethodInvocation invocation) => true;

        public IMethodReturn Execute(IMethodInvocation invocation, ExecuteHandler next)
        {
            var result = next.Invoke(invocation, next);
            output.WriteLine(result.ToString());
            return result;
        }
    }

    public class OutputHelperLoggingTests
    {
        readonly ITestOutputHelper output;

        public OutputHelperLoggingTests(ITestOutputHelper output) => this.output = output;

        /// <summary>
        /// Running this test will render all the calls in the output window
        /// </summary>
        //void TurnOn()
        //int Add(int x: 3, int y: 5) => 0
        //void Store(string name: "m1", int value: 42)
        //int? Recall(string name: "m1") => null
        //void TurnOff()
        [Fact]
        public void LogsAllCalls()
        {
            var calc = Stunt.Of<ICalculator>()
                .AddBehavior(new OutputHelperLoggingBehavior(output))
                .AddBehavior(new DefaultValueBehavior());

            calc.TurnOn();
            calc.Add(3, 5);
            calc.Store("m1", 42);
            calc.Recall("m1");
            calc.TurnOff();
        }
    }
}
