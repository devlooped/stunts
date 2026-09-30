#pragma warning disable IDE0079
#pragma warning disable CS0436
using System;
using Xunit;

namespace Stunts.Scenario.ValueTypeNull
{
    public interface IValues
    {
        int GetValue();

        int GetValue(ref int value);

        bool TryGetValue(out int value);
    }

    public delegate int ValueDelegate(ref int value);

    public class Test : IRunnable
    {
        public void Run()
        {
            NullReturnThrowsDescriptiveArgumentNullException();
            NullReturnWithRefArgumentThrowsDescriptiveArgumentNullException();
            NullDelegateReturnWithRefArgumentThrowsDescriptiveArgumentNullException();
            NullOutputThrowsDescriptiveArgumentNullException();
        }

        static void NullReturnThrowsDescriptiveArgumentNullException()
        {
            var stunt = Stunt.Of<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null));

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                stunt.GetValue();
            });

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullReturnWithRefArgumentThrowsDescriptiveArgumentNullException()
        {
            var stunt = Stunt.Of<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null, invocation.Arguments.Get<int>("value")));
            var value = 1;

            var exception = Assert.Throws<ArgumentNullException>(() => stunt.GetValue(ref value));

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullDelegateReturnWithRefArgumentThrowsDescriptiveArgumentNullException()
        {
            var stunt = Stunt.Of<ValueDelegate>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null, invocation.Arguments.Get<int>("value")));
            var value = 1;

            var exception = Assert.Throws<ArgumentNullException>(() => stunt(ref value));

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullOutputThrowsDescriptiveArgumentNullException()
        {
            var stunt = Stunt.Of<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(true, (object)null));

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                stunt.TryGetValue(out _);
            });

            Assert.Equal("value", exception.ParamName);
            Assert.Contains("value", exception.Message);
            Assert.Contains("int", exception.Message);
        }
    }
}
