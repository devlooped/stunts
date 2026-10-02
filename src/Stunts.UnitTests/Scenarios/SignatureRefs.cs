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

        public virtual bool TrySum(ReadOnlySpan<int> data, out int sum)
        {
            sum = 0;
            foreach (var value in data)
                sum += value;
            return true;
        }

        public virtual ReadOnlySpan<int> Values => new[] { 1, 2 };
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

        public virtual int ReadAndAdvance(ref int* value)
        {
            var result = *value;
            value++;
            return result;
        }

        public virtual void Reset(out int* value) => value = null;
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
            SpanAndOutArgumentsRoundTrip();
            RefStructPropertyReturnsThroughHolder();
            PointerOutputsRoundTrip();
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

        static void SpanAndOutArgumentsRoundTrip()
        {
            var stunt = Stunt.For<Buffer>();
            Buffer buffer = stunt.ToObject();
            Assert.True(buffer.TrySum(new[] { 2, 3 }, out var sum));
            Assert.Equal(5, sum);

            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(false,
                invocation.Arguments.SetValue("sum", 42)), invocation => invocation.MethodBase.Name == nameof(Buffer.TrySum));
            Assert.False(buffer.TrySum(new[] { 1 }, out sum));
            Assert.Equal(42, sum);
        }

        static void RefStructPropertyReturnsThroughHolder()
        {
            Buffer buffer = Stunt.Of<Buffer>();
            Assert.Equal(2, buffer.Values.Length);
            Assert.Equal(1, buffer.Values[0]);
        }

        static void PointerOutputsRoundTrip()
        {
            int* values = stackalloc int[] { 3, 5 };
            var current = values;
            var stunt = Stunt.For<Reader>();
            Reader reader = stunt.ToObject();
            Assert.Equal(3, reader.ReadAndAdvance(ref current));
            Assert.Equal(5, *current);
            reader.Reset(out current);
            Assert.Equal((nint)0, (nint)current);

            var address = (nint)values;
            stunt.AddBehavior((invocation, next) => invocation.CreateValueReturn(null,
                invocation.Arguments.SetValue("value", new PointerRef((void*)address))),
                invocation => invocation.MethodBase.Name == nameof(Reader.Reset));
            reader.Reset(out current);
            Assert.Equal(address, (nint)current);
        }
    }
}
