using System;
using System.Reflection;
using Sample;
using Xunit;

namespace Stunts.UnitTests
{
    public class MethodInvocationTests
    {
        public void Do() { }

        [Fact]
        public void TestDo()
        {
            var invocation = new MethodInvocation(this, typeof(MethodInvocationTests).GetMethod(nameof(Do))!);

            var actual = invocation.ToString();

            Assert.Equal("void Do()", actual);
        }

        public void DoWithInt(int value) { }

        [Fact]
        public void TestDoWithInt()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithInt))!, 5);

            var actual = invocation.ToString();

            Assert.Equal("void DoWithInt(int value: 5)", actual);
        }

        [Fact]
        public void EqualIfTargetMethodAndArgumentsMatch()
        {
            var doThis = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(Do))!);
            var doThiss = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(Do))!);

            Assert.Equal((object)doThis, doThiss);
            Assert.Equal(doThis, doThiss);
            Assert.Equal(doThis.GetHashCode(), doThiss.GetHashCode());
            Assert.True(doThis.Equals(doThiss));
            Assert.True(doThis.Equals((object)doThiss));

            var doOther = MethodInvocation.Create(new MethodInvocationTests(), typeof(MethodInvocationTests).GetMethod(nameof(Do))!);

            Assert.NotEqual(doThis, doOther);

            var doInt5 = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithInt))!, 5);
            var doInt5s = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithInt))!, 5);

            Assert.NotEqual(doThis, doInt5);
            Assert.Equal(doInt5, doInt5s);
            Assert.Equal(doInt5.GetHashCode(), doInt5s.GetHashCode());

            var doInt6 = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithInt))!, 6);

            Assert.NotEqual(doInt5, doInt6);

            var doIntNull = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithNullableInt))!, 5);
            var doIntNulls = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithNullableInt))!, default(int?));

            Assert.NotEqual(doIntNull, doIntNulls);
            Assert.NotEqual(doIntNull.GetHashCode(), doIntNulls.GetHashCode());
        }

        public void DoWithNullableInt(int? value) { }

        [Fact]
        public void TestDoWithNullableInt()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithNullableInt))!, 5);

            var actual = invocation.ToString();

            Assert.Equal("void DoWithNullableInt(int? value: 5)", actual);
        }

        public void DoWithNullableIntNull(int? value) { }

        [Fact]
        public void TestDoWithNullableIntNull()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithNullableIntNull))!, default(int?));

            var actual = invocation.ToString();

            Assert.Equal("void DoWithNullableIntNull(int? value: null)", actual);
        }

        public void DoWithString(string value) { }

        [Fact]
        public void TestDoWithString()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithString))!, "foo");

            var actual = invocation.ToString();

            Assert.Equal("void DoWithString(string value: \"foo\")", actual);
        }

        public void DoWithNullString(string value) { }

        [Fact]
        public void TestDoWithNullString()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoWithNullString))!, default(string));

            var actual = invocation.ToString();

            Assert.Equal("void DoWithNullString(string value: null)", actual);
        }

        public bool DoReturn() => true;

        [Fact]
        public void TestDoReturn()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoReturn))!);

            var actual = invocation.ToString();

            Assert.Equal("bool DoReturn()", actual);
        }

        public void DoRef(ref int i) { }

        [Fact]
        public void TestDoRef()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoRef))!, 5);

            var actual = invocation.ToString();

            Assert.Equal("void DoRef(ref int i: 5)", actual);
        }

        public void DoOut(out int value) { value = 5; }

        [Fact]
        public void TestDoOut()
        {
            var invocation = MethodInvocation.Create(this, typeof(MethodInvocationTests).GetMethod(nameof(DoOut))!, 5);

            var actual = invocation.ToString();

            Assert.Equal("void DoOut(out int value)", actual);
        }

        [Fact]
        public void ThrowsIfNullTarget()
            => Assert.Throws<ArgumentNullException>(() => new MethodInvocation(null!, MethodBase.GetCurrentMethod()!));

        [Fact]
        public void ThrowsIfNullMethodBase()
            => Assert.Throws<ArgumentNullException>(() => new MethodInvocation(this, null!));

        [Fact]
        public void ReturnOutputsContainsRefAndOut()
        {
            var calculator = new Calculator();
            var invocation = MethodInvocation.Create(calculator, typeof(ICalculator).GetMethod(nameof(ICalculator.TryAdd))!, 2, 3, 5);

            var result = invocation.CreateValueReturn(true);

            Assert.Equal(3, result.Outputs.Count);
        }
    }
}
