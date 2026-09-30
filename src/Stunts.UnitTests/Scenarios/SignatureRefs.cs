#pragma warning disable CS0436
using System;
using System.Reflection;
using Xunit;

namespace Stunts.Scenarios.SignatureRefs
{
    public class Counter
    {
        int value = 7;

        public virtual ref readonly int Current() => ref value;

        public virtual void Add(ref readonly int amount) => value += amount;
    }

    public class Buffer
    {
        public virtual int Sum(Span<int> data)
        {
            var total = 0;
            foreach (var item in data)
                total += item;
            return total;
        }

        public virtual Span<int> First(Span<int> data) => data[..1];
    }

    public ref struct Token
    {
        public int Value;
        public Token(int value) => Value = value;
    }

    public class Parser
    {
        public virtual int Read(Token token) => token.Value;
    }

    public unsafe class Reader
    {
        public virtual int Read(int* value) => *value;
    }

    public unsafe class Test : IRunnable
    {
        public void Run()
        {
            RefReadonlyKeepsTheModifierAndTheValue();
            SpanIsVisibleToTheBehavior();
            SpanReturnProceedsThroughStructRef();
            CustomRefStructRoundTrips();
            PointerIsVisibleToTheBehavior();
        }

        static void RefReadonlyKeepsTheModifierAndTheValue()
        {
            var stunt = Stunt.Of<Counter>();
            Counter counter = stunt;

            ref readonly var current = ref counter.Current();
            Assert.Equal(7, current);

            var amount = 3;
            counter.Add(in amount);
            Assert.Equal(10, counter.Current());

            var type = counter.GetType();
            Assert.Equal(
                typeof(Counter).GetMethod(nameof(Counter.Current))!.ReturnParameter.GetRequiredCustomModifiers(),
                type.GetMethod(nameof(Counter.Current))!.ReturnParameter.GetRequiredCustomModifiers());
            Assert.Equal(
                typeof(Counter).GetMethod(nameof(Counter.Add))!.GetParameters()[0].GetRequiredCustomModifiers(),
                type.GetMethod(nameof(Counter.Add))!.GetParameters()[0].GetRequiredCustomModifiers());
        }

        static void SpanIsVisibleToTheBehavior()
        {
            var stunt = Stunt.For<Buffer>();
            stunt.AddBehavior((invocation, next) =>
            {
                var span = (SpanRef<int>)invocation.Arguments.GetValue("data")!;
                span.Value[0] = 10;
                return next(invocation, next);
            }, invocation => !invocation.MethodBase.IsConstructor);

            Buffer buffer = stunt.ToObject();

            Assert.Equal(15, buffer.Sum(new[] { 1, 2, 3 }));
        }

        static void SpanReturnProceedsThroughStructRef()
        {
            var stunt = Stunt.For<Buffer>();
            stunt.AddBehavior((invocation, next) => next(invocation, next));

            Buffer buffer = stunt.ToObject();

            var data = new[] { 4, 5 };
            Assert.Equal(4, buffer.First(data)[0]);
        }

        static void CustomRefStructRoundTrips()
        {
            var stunt = Stunt.For<Parser>();
            stunt.AddBehavior((invocation, next) =>
            {
                var token = (StructRef<Token>)invocation.Arguments.GetValue("token")!;
                Assert.Equal(1, token.Value.Value);
                token.Value = new Token(4);
                return next(invocation, next);
            }, invocation => !invocation.MethodBase.IsConstructor);

            Parser parser = stunt.ToObject();

            Assert.Equal(4, parser.Read(new Token(1)));
        }

        static void PointerIsVisibleToTheBehavior()
        {
            var stunt = Stunt.For<Reader>();
            stunt.AddBehavior((invocation, next) =>
            {
                var pointer = (PointerRef)invocation.Arguments.GetValue("value")!;
                unsafe
                {
                    *(int*)pointer.Value = 9;
                }

                return next(invocation, next);
            }, invocation => !invocation.MethodBase.IsConstructor);

            var value = 4;
            Reader reader = stunt.ToObject();
            unsafe
            {
                Assert.Equal(9, reader.Read(&value));
            }

            Assert.Equal(9, value);
        }
    }
}
