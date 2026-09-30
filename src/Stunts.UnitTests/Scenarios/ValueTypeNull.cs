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
            IValues values = Stunt.For<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null)).ToObject();

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                values.GetValue();
            });

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullReturnWithRefArgumentThrowsDescriptiveArgumentNullException()
        {
            IValues values = Stunt.For<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null, invocation.Arguments.Get<int>("value")),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();
            var value = 1;

            var exception = Assert.Throws<ArgumentNullException>(() => values.GetValue(ref value));

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullDelegateReturnWithRefArgumentThrowsDescriptiveArgumentNullException()
        {
            ValueDelegate values = Stunt.For<ValueDelegate>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn((object)null, invocation.Arguments.Get<int>("value")),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();
            var value = 1;

            var exception = Assert.Throws<ArgumentNullException>(() => values(ref value));

            Assert.Equal(nameof(IMethodReturn.ReturnValue), exception.ParamName);
            Assert.Contains(nameof(IMethodReturn.ReturnValue), exception.Message);
            Assert.Contains("int", exception.Message);
        }

        static void NullOutputThrowsDescriptiveArgumentNullException()
        {
            IValues values = Stunt.For<IValues>().AddBehavior((invocation, next) =>
                invocation.CreateValueReturn(true, (object)null),
                invocation => !invocation.MethodBase.IsConstructor).ToObject();

            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                values.TryGetValue(out _);
            });

            Assert.Equal("value", exception.ParamName);
            Assert.Contains("value", exception.Message);
            Assert.Contains("int", exception.Message);
        }
    }
}
