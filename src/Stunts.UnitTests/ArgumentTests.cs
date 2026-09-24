using System;
using TypeNameFormatter;
using Xunit;

namespace Stunts.UnitTests
{
    public class ArgumentTests
    {
        [Fact]
        public void WhenObjectValueIsCompatible_ThenRawValueEqualsValue()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[4];
            var value = new object();

            var argument = new ObjectArgument(parameter, value);

            Assert.Same(value, argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueIsNotCompatible_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            Assert.Throws<ArgumentException>(() => new ObjectArgument(parameter, 42));
        }

        [Fact]
        public void WhenObjectValueIsNull_ThenSucceedsForReferenceType()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = new ObjectArgument(parameter, null);

            Assert.Null(argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueIsCompatible_ThenSucceedsForRefParameter()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[3];

            var argument = new ObjectArgument(parameter, "foo");

            Assert.Equal("foo", argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueIsNotNull_ThenSucceedsForOutParameter()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[5];

            var argument = new ObjectArgument(parameter, PlatformID.Win32NT);

            Assert.Equal(PlatformID.Win32NT, argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueIsNullForOutNullable_ThenSucceeds()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[5];

            var argument = new ObjectArgument(parameter, null);

            Assert.Null(argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueIsNullForValueType_ThenThrowsArgumentNullException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];

            Assert.Throws<ArgumentNullException>(() => new ObjectArgument(parameter, null));
        }

        [Fact]
        public void WhenObjectValueIsNullForNullableValueType_ThenSucceeds()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[1];

            var argument = new ObjectArgument(parameter, null);

            Assert.Null(argument.RawValue);
        }

        [Fact]
        public void WhenObjectValueAssignedNewValue_ThenNewInstanceReturned()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[4];
            var value = new object();

            var argument = new ObjectArgument(parameter, value);

            var updated = argument.WithRawValue(new object());

            Assert.NotSame(argument, updated);
            Assert.NotSame(argument.RawValue, updated.RawValue);
        }

        [Fact]
        public void WhenObjectValueAssignedIncompatibleNewValue_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];
            var argument = new ObjectArgument(parameter, 42);

            Assert.Throws<ArgumentException>(() => argument.WithRawValue("foo"));
        }

        [Fact]
        public void WhenComparingArguments_ThenComparesStructurally()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];
            var first = new ObjectArgument(parameter, 42);
            var second = new ObjectArgument(parameter, 42);

            Assert.Equal(first, second);
            Assert.NotEqual(first, second.WithRawValue(24));
        }







        [Fact]
        public void WhenTypedValueIsCompatible_ThenRawValueEqualsValue()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = Argument.Create(parameter, "foo");

            Assert.Equal("foo", argument.Value);
        }

        [Fact]
        public void WhenTypedValueIsNotCompatible_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            Assert.Throws<ArgumentException>(() => Argument.Create(parameter, 42));
        }

        [Fact]
        public void WhenTypeIsNotCompatible_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];

            Assert.Throws<ArgumentException>(() => Argument.Create(parameter, "foo"));
        }

        [Fact]
        public void WhenTypeIsNullableValueTypeButParameterIsNot_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];

            Assert.Throws<ArgumentException>(() => Argument.Create<int?>(parameter, 42));
        }

        [Fact]
        public void WhenTypeIsNotNullableValueTypeButParameterIs_ThenSucceeds()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[1];

            var argument = Argument.Create(parameter, 42);

            Assert.Equal(42, argument.Value);
        }

        [Fact]
        public void WhenTypedValueIsNull_ThenSucceedsForReferenceType()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = new ObjectArgument(parameter, null);

            Assert.Null(argument.RawValue);
        }

        [Fact]
        public void WhenTypedValueIsNullForNullableValueType_ThenSucceeds()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[1];

            var argument = new ObjectArgument(parameter, null);

            Assert.Null(argument.RawValue);
        }

        [Fact]
        public void WhenTypedValueAssignedNewValue_ThenNewInstanceReturned()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = Argument.Create(parameter, "foo");

            var updated = argument.WithValue("bar");

            Assert.NotEqual(argument, updated);
            Assert.NotEqual(argument.RawValue, updated.RawValue);
        }

        [Fact]
        public void WhenTypedValueAssignedIncompatibleRawValue_ThenThrowsArgumentException()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = Argument.Create(parameter, "foo");

            Assert.Throws<ArgumentException>(() => argument.WithRawValue(42));
        }

        [Fact]
        public void WhenTypedValueAssignedNullRawValue_ThenSucceedsForReferenceType()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[2];

            var argument = Argument.Create(parameter, "foo");

            Assert.Null(argument.WithRawValue(null).RawValue);
        }

        [Fact]
        public void WhenComparingTypedArguments_ThenComparesStructurally()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];
            var first = Argument.Create(parameter, 42);
            var second = Argument.Create(parameter, 42);

            Assert.Equal(first, second);
            Assert.NotEqual(first, second.WithRawValue(24));
        }

        [Fact]
        public void WhenUpdatingValue_ThenCanUseRecordsSyntax()
        {
            var parameter = typeof(ArgumentTests).GetMethod(nameof(Arguments)).GetParameters()[0];

            var first = Argument.Create(parameter, 42);
            var second = first with { Value = 24 };

            Assert.Equal(24, second.Value);
        }


#pragma warning disable xUnit1013 // Public method should be marked as test
        public void Arguments(int arg0, int? arg1, string arg2, ref string? arg3, object arg4, out PlatformID? arg5)
        {
            arg5 = default;
        }
    }
}
