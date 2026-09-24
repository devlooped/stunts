using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Stunts.UnitTests
{
    public class RefReturns
    {
        [Fact]
        public void CanReturnRef()
        {
            INumbers stunt = new NumbersStunt();


            Ref<int> value = 25;

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(value, invocation.Arguments));

            var index = 0;
            ref int v = ref stunt.NumberAt(ref index, out var count);

            v = 42;

            Assert.Equal(42, value.Value);
            Assert.True(42 == value);
        }

        public interface INumbers
        {
            ref int NumberAt(ref int index, out int count);
        }

        public class NumbersStunt : INumbers, IStunt
        {
            BehaviorPipeline pipeline = new();

            public IList<IStuntBehavior> Behaviors => pipeline.Behaviors;

            public ref int NumberAt(ref int index, out int count)
            {
                count = default;
                var local_index = index;

                var returns = pipeline.Execute(MethodInvocation.Create(this, MethodBase.GetCurrentMethod(), local_index, count));
                index = returns.Outputs.GetNullable<int>("index");
                count = returns.Outputs.GetNullable<int>("count");

                return ref ((Ref<int>)returns.ReturnValue).Value;
            }
        }
    }
}
